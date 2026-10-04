using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RPG_Harness.Services;

/// <summary>
/// RPG-состояние кампании, привязанное к чату (персонаж, инвентарь, NPC, противники, города).
/// Хранится в {WorkDirectory}/rpg/{chatId}.json и зеркалится в markdown-файлы кампании
/// в {WorkDirectory}/campaigns/{папка чата}/: Hero.md, Inventory.md, Characters/, Adversaries.md,
/// Cities.md, Quests.md, Story_Arc.md, Worldstate.md, а также шаблоны мастера (CampaignTemplates):
/// World.md, Rules.md, Factions.md, Bestiary.md, Secrets.md, Journal.md.
/// У каждого чата своя папка — кампании в одной рабочей директории не перезаписывают друг друга.
/// </summary>
public sealed class RpgStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly SettingsService _settings;
    private readonly ChatStore _chats;
    private readonly ItemEconomy _economy;
    private readonly ItemCatalog _catalog;
    private readonly object _lock = new();
    private readonly Dictionary<string, RpgState> _cache = new();
    private readonly HashSet<string> _scaffolded = new();
    private readonly Dictionary<string, HashSet<string>> _writtenNpcFiles = new();
    private readonly Dictionary<string, HashSet<string>> _writtenPartyFiles = new();

    /// <summary>Последнее содержимое Hero.md, записанное нами (для обнаружения ручных правок человека).</summary>
    private readonly Dictionary<string, string> _lastHeroMd = new();

    /// <summary>Последний снимок книги героя на чат: (индекс сообщения игрока, JSON состояния).</summary>
    private readonly Dictionary<string, (int Index, string Json)> _snapshots = new();

    public RpgStateStore(SettingsService settings, ChatStore chats, ItemEconomy economy, ItemCatalog catalog)
    {
        _settings = settings;
        _chats = chats;
        _economy = economy;
        _catalog = catalog;
        _settings.Saved += OnSettingsSaved;
        _chats.Changed += OnChatsChanged;
    }

    /// <summary>Видимость правой панели (UI-состояние сессии, не сохраняется).</summary>
    public bool PanelOpen { get; private set; } = true;

    /// <summary>Изменение видимости панели (для перерисовки layout).</summary>
    public event Action? PanelChanged;

    /// <summary>Последняя ошибка записи на диск (null — ошибок не было).</summary>
    public string? LastError { get; private set; }

    private string? RpgDirectory
    {
        get
        {
            var dir = _settings.Current.WorkDirectory;
            return string.IsNullOrWhiteSpace(dir) ? null : Path.Combine(dir, "rpg");
        }
    }

    private string? WorkRoot
    {
        get
        {
            var dir = _settings.Current.WorkDirectory;
            return string.IsNullOrWhiteSpace(dir) ? null : dir;
        }
    }

    /// <summary>
    /// Папка кампании чата (создаётся при первом обращении). Файловые инструменты модели ограничены ею.
    /// null — рабочая директория не выбрана.
    /// </summary>
    public string? CampaignDirectory(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || WorkRoot is null)
        {
            return null;
        }

        return CampaignDirectory(sessionId, GetOrCreate(sessionId));
    }

    private string? CampaignDirectory(string sessionId, RpgState state)
    {
        var root = WorkRoot;
        if (root is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(state.CampaignFolder))
        {
            // Имя папки фиксируется один раз: «Имя героя-abc123». Переименование героя папку не двигает.
            var hero = string.IsNullOrWhiteSpace(state.Character.Name) ? Lang.T("Кампания", "Campaign") : state.Character.Name;
            state.CampaignFolder = $"{SafeFileName(hero)}-{sessionId[..Math.Min(6, sessionId.Length)]}";
            SaveJson(sessionId, state);
        }
        else
        {
            // Поле приходит с диска: старое или вручную изменённое сохранение не должно
            // превратить имя папки в абсолютный путь либо выход через ../.
            var safeFolder = SafeFileName(state.CampaignFolder);
            if (!string.Equals(safeFolder, state.CampaignFolder, StringComparison.Ordinal))
            {
                state.CampaignFolder = safeFolder;
                SaveJson(sessionId, state);
            }
        }

        var dir = Path.Combine(root, "campaigns", state.CampaignFolder);
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }

        return dir;
    }

    /// <summary>Мир создан: World.md существует и уже не шаблон.</summary>
    public bool IsWorldCreated(string? sessionId)
    {
        var dir = CampaignDirectory(sessionId);
        var path = dir is null ? null : Path.Combine(dir, "World.md");
        try
        {
            return path is not null && File.Exists(path) && !CampaignTemplates.IsUnfilled("World.md", File.ReadAllText(path));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Хвост журнала кампании (последние maxChars символов) или null.</summary>
    public string? ReadJournalTail(string? sessionId, int maxChars)
    {
        var dir = CampaignDirectory(sessionId);
        var path = dir is null ? null : Path.Combine(dir, "Journal.md");
        try
        {
            if (path is null || !File.Exists(path))
            {
                return null;
            }

            var text = File.ReadAllText(path).Trim();
            return text.Length <= maxChars ? text : "…" + text[^maxChars..];
        }
        catch
        {
            return null;
        }
    }

    public RpgState GetOrCreate(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return new RpgState();
        }

        lock (_lock)
        {
            if (_cache.TryGetValue(sessionId, out var state))
            {
                return state;
            }

            state = LoadFromDisk(sessionId);
            _cache[sessionId] = state;
            AdoptHeroMd(sessionId, state);
            if (ApplyHpScaleMigration(state) | ApplyNearbyPlaceMigration(state))
            {
                Save(sessionId, state);
            }
            return state;
        }
    }

    /// <summary>
    /// При загрузке состояния запоминает текущий Hero.md как «наш»: правки, сделанные руками, пока харнес
    /// не работал, переносятся сразу, а первое сохранение после запуска уже не откатит свежие изменения
    /// (например, золото после сделки) к старому содержимому файла.
    /// </summary>
    private void AdoptHeroMd(string sessionId, RpgState state)
    {
        try
        {
            var root = CampaignDirectory(sessionId, state);
            var path = root is null ? null : Path.Combine(root, "Hero.md");
            if (path is null || !File.Exists(path))
            {
                return;
            }

            var current = File.ReadAllText(path);
            if (current != BuildHeroMd(state.Character))
            {
                TryParseHeroMd(current, state.Character);
            }

            _lastHeroMd[sessionId] = current;
        }
        catch
        {
            // файл недоступен — импорт при следующем сохранении
        }
    }

    /// <summary>Вызывается после любого сохранения состояния (для перерисовки панели).</summary>
    public event Action<string>? StateChanged;

    public void Save(string? sessionId, RpgState state)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || state is null)
        {
            return;
        }

        SaveJson(sessionId, state);

        // Файлы кампании следуют за правками состояния.
        SyncCampaignFiles(sessionId, state);
        StateChanged?.Invoke(sessionId);
    }

    private void SaveJson(string sessionId, RpgState state)
    {
        var dir = RpgDirectory;
        if (dir is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, sessionId + ".json"), JsonSerializer.Serialize(state, JsonOptions));
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    /// <summary>
    /// Создаёт файлы кампании (Hero.md, Inventory.md, Characters/, World.md, Rules.md…),
    /// если их ещё нет. Вызывается при открытии/создании чата; существующие файлы не перезаписывает.
    /// </summary>
    public void EnsureCampaignFiles(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        var state = GetOrCreate(sessionId);

        lock (_lock)
        {
            if (_scaffolded.Contains(sessionId))
            {
                return;
            }

            var root = CampaignDirectory(sessionId, state);
            if (root is null)
            {
                return;
            }

            try
            {
                WriteIfMissing(Path.Combine(root, "Hero.md"), BuildHeroMd(state.Character));
                WriteIfMissing(Path.Combine(root, "Inventory.md"), BuildInventoryMd(state));

                var charsDir = Path.Combine(root, "Characters");
                Directory.CreateDirectory(charsDir);
                var partyDir = Path.Combine(charsDir, "Party");
                Directory.CreateDirectory(partyDir);

                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Notables.md", "Candidates.md" };
                foreach (var npc in state.ImportantCharacters)
                {
                    var fileName = UniqueFileName(used, SafeFileName(npc.Name));
                    WriteIfMissing(Path.Combine(charsDir, fileName), BuildNpcMd(npc, _economy));
                }

                _writtenNpcFiles[sessionId] = used;

                WriteIfMissing(Path.Combine(charsDir, "Notables.md"), BuildNotablesMd(state.Notables));
                WriteIfMissing(Path.Combine(charsDir, "Candidates.md"), BuildCandidatesMd(state.CompanionCandidates));
                var partyUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var member in state.Party.Take(3))
                {
                    var fileName = UniqueFileName(partyUsed, SafeFileName(member.Name));
                    WriteIfMissing(Path.Combine(partyDir, fileName), BuildPartyMemberMd(member));
                }
                _writtenPartyFiles[sessionId] = partyUsed;

                WriteIfMissing(Path.Combine(root, "Adversaries.md"), BuildAdversariesMd(state.Adversaries));
                WriteIfMissing(Path.Combine(root, "Cities.md"), BuildCitiesMd(state.Cities));
                WriteIfMissing(Path.Combine(root, "Quests.md"), BuildQuestsMd(state.Quests));
                WriteIfMissing(Path.Combine(root, "Story_Arc.md"), BuildStoryArcMd(state.StoryArcs));
                WriteIfMissing(Path.Combine(root, "Instances.md"), BuildInstancesMd(state));
                WriteIfMissing(Path.Combine(root, "Worldstate.md"), BuildWorldstateMd(state));
                WriteMasterTemplates(root);

                _scaffolded.Add(sessionId);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
        }
    }

    /// <summary>Удаляет RPG-заметки чата (используется при зачистке осиротевших файлов).</summary>
    // ===== Снимок перед ходом игрока («Переиграть ход» / «Изменить ход») =====

    /// <summary>
    /// Запоминает книгу героя перед ходом игрока: messageIndex — индекс его сообщения в чате.
    /// Хранится один (последний) снимок на чат, в памяти и в rpg/&lt;id&gt;.undo — переживает перезапуск.
    /// </summary>
    public void SaveSnapshot(string sessionId, int messageIndex)
    {
        var json = JsonSerializer.Serialize(GetOrCreate(sessionId), JsonOptions);
        lock (_lock)
        {
            _snapshots[sessionId] = (messageIndex, json);
        }

        var dir = RpgDirectory;
        if (dir is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, sessionId + ".undo"), messageIndex + "\n" + json);
        }
        catch
        {
            // снимок — удобство, не критичный путь
        }
    }

    /// <summary>Есть ли снимок книги героя, сделанный перед сообщением с этим индексом.</summary>
    public bool HasSnapshot(string sessionId, int messageIndex) => ReadSnapshot(sessionId) is { } s && s.Index == messageIndex;

    /// <summary>Откатывает книгу героя к снимку перед сообщением messageIndex. false — снимка нет.</summary>
    public bool RestoreSnapshot(string sessionId, int messageIndex)
    {
        if (ReadSnapshot(sessionId) is not { } snap || snap.Index != messageIndex)
        {
            return false;
        }

        RpgState? state;
        try
        {
            state = JsonSerializer.Deserialize<RpgState>(snap.Json, JsonOptions);
        }
        catch
        {
            return false;
        }

        if (state is null)
        {
            return false;
        }

        ApplyHpScaleMigration(state);
        ApplyNearbyPlaceMigration(state);

        lock (_lock)
        {
            _cache[sessionId] = state;
        }

        Save(sessionId, state);
        return true;
    }

    /// <summary>Однократно повышает ХП старых сохранений, сохраняя долю текущего здоровья.</summary>
    private static bool ApplyHpScaleMigration(RpgState state)
    {
        if (state.HpScaleVersion >= 1)
        {
            return false;
        }

        if (state.Character.HpMax > 0)
        {
            state.Character.HpMax *= 2;
            state.Character.HpCurrent *= 2;
        }

        foreach (var member in state.Party)
        {
            if (member.HpMax <= 0) continue;
            member.HpMax = Progression.ScaleCompanionHp(member.HpMax);
            member.HpCurrent = Progression.ScaleCompanionHp(member.HpCurrent);
        }

        state.HpScaleVersion = 1;
        return true;
    }

    /// <summary>
    /// Восстанавливает сохранения, где nearby-NPC были созданы до первого назначения ScenePlace.
    /// Пустая привязка при уже известном месте сцены нарушает инвариант: такой NPC нигде не виден.
    /// </summary>
    private static bool ApplyNearbyPlaceMigration(RpgState state)
    {
        if (string.IsNullOrWhiteSpace(state.ScenePlace))
        {
            return false;
        }

        var changed = false;
        foreach (var npc in state.ImportantCharacters.Where(n =>
                     n.Category == "nearby" && string.IsNullOrWhiteSpace(n.NearbyPlace)))
        {
            npc.NearbyPlace = state.ScenePlace;
            changed = true;
        }

        return changed;
    }

    private (int Index, string Json)? ReadSnapshot(string sessionId)
    {
        lock (_lock)
        {
            if (_snapshots.TryGetValue(sessionId, out var mem))
            {
                return mem;
            }
        }

        var dir = RpgDirectory;
        var file = dir is null ? null : Path.Combine(dir, sessionId + ".undo");
        if (file is null || !File.Exists(file))
        {
            return null;
        }

        try
        {
            var text = File.ReadAllText(file);
            var nl = text.IndexOf('\n');
            if (nl <= 0 || !int.TryParse(text[..nl], out var index))
            {
                return null;
            }

            var snap = (index, text[(nl + 1)..]);
            lock (_lock)
            {
                _snapshots[sessionId] = snap;
            }

            return snap;
        }
        catch
        {
            return null;
        }
    }

    public void Delete(string sessionId)
    {
        lock (_lock)
        {
            _cache.Remove(sessionId);
            _scaffolded.Remove(sessionId);
            _writtenNpcFiles.Remove(sessionId);
            _writtenPartyFiles.Remove(sessionId);
            _lastHeroMd.Remove(sessionId);
            _snapshots.Remove(sessionId);
        }

        var dir = RpgDirectory;
        if (dir is null)
        {
            return;
        }

        try
        {
            var file = Path.Combine(dir, sessionId + ".json");
            if (File.Exists(file))
            {
                File.Delete(file);
            }

            var undo = Path.Combine(dir, sessionId + ".undo");
            if (File.Exists(undo))
            {
                File.Delete(undo);
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    public void TogglePanel() => SetPanelOpen(!PanelOpen);

    public void SetPanelOpen(bool open)
    {
        if (PanelOpen == open)
        {
            return;
        }

        PanelOpen = open;
        PanelChanged?.Invoke();
    }

    /// <summary>
    /// Перезаписывает зеркало кампании в markdown под текущее состояние.
    /// World.md и Rules.md не трогает (их ведут пользователь и модель через file tools).
    /// Удаляет только файлы NPC, ранее записанные этим же чатом.
    /// </summary>
    private void SyncCampaignFiles(string sessionId, RpgState state)
    {
        var root = CampaignDirectory(sessionId, state);
        if (root is null)
        {
            return;
        }

        try
        {
            // Если человек правил Hero.md руками — переносим его правки в состояние,
            // прежде чем перезаписать файл (панель и файл не должны расходиться).
            var heroPath = Path.Combine(root, "Hero.md");
            ImportHeroMdIfChanged(sessionId, heroPath, state.Character);

            var heroMd = BuildHeroMd(state.Character);
            File.WriteAllText(heroPath, heroMd);
            _lastHeroMd[sessionId] = heroMd;
            File.WriteAllText(Path.Combine(root, "Inventory.md"), BuildInventoryMd(state));
            File.WriteAllText(Path.Combine(root, "Adversaries.md"), BuildAdversariesMd(state.Adversaries));
            File.WriteAllText(Path.Combine(root, "Cities.md"), BuildCitiesMd(state.Cities));
            File.WriteAllText(Path.Combine(root, "Quests.md"), BuildQuestsMd(state.Quests));
            File.WriteAllText(Path.Combine(root, "Story_Arc.md"), BuildStoryArcMd(state.StoryArcs));
            File.WriteAllText(Path.Combine(root, "Instances.md"), BuildInstancesMd(state));
            File.WriteAllText(Path.Combine(root, "Worldstate.md"), BuildWorldstateMd(state));

            // Шаблоны, которые дальше ведёт мастер, — только если их ещё нет
            // (первое сохранение случается при создании героя, раньше, чем EnsureCampaignFiles).
            WriteMasterTemplates(root);

            var charsDir = Path.Combine(root, "Characters");
            Directory.CreateDirectory(charsDir);
            var partyDir = Path.Combine(charsDir, "Party");
            Directory.CreateDirectory(partyDir);
            File.WriteAllText(Path.Combine(charsDir, "Notables.md"), BuildNotablesMd(state.Notables));
            File.WriteAllText(Path.Combine(charsDir, "Candidates.md"), BuildCandidatesMd(state.CompanionCandidates));

            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Notables.md", "Candidates.md" };
            foreach (var npc in state.ImportantCharacters)
            {
                var fileName = UniqueFileName(written, SafeFileName(npc.Name));
                File.WriteAllText(Path.Combine(charsDir, fileName), BuildNpcMd(npc, _economy));
            }

            lock (_lock)
            {
                if (_writtenNpcFiles.TryGetValue(sessionId, out var previous))
                {
                    foreach (var stale in previous.Except(written, StringComparer.OrdinalIgnoreCase).ToList())
                    {
                        var path = Path.Combine(charsDir, stale);
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                }

                _writtenNpcFiles[sessionId] = written;
                var partyWritten = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var member in state.Party.Take(3))
                {
                    var fileName = UniqueFileName(partyWritten, SafeFileName(member.Name));
                    File.WriteAllText(Path.Combine(partyDir, fileName), BuildPartyMemberMd(member));
                }
                if (_writtenPartyFiles.TryGetValue(sessionId, out var previousParty))
                {
                    foreach (var stale in previousParty.Except(partyWritten, StringComparer.OrdinalIgnoreCase))
                    {
                        var path = Path.Combine(partyDir, stale);
                        if (File.Exists(path)) File.Delete(path);
                    }
                }
                _writtenPartyFiles[sessionId] = partyWritten;
                _scaffolded.Add(sessionId);
            }

            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    private RpgState LoadFromDisk(string sessionId)
    {
        var dir = RpgDirectory;
        var file = dir is null ? null : Path.Combine(dir, sessionId + ".json");
        if (file is not null && File.Exists(file))
        {
            try
            {
                var state = JsonSerializer.Deserialize<RpgState>(File.ReadAllText(file), JsonOptions);
                if (state is not null)
                {
                    ItemStats.Backfill(state);
                    // Старые сохранения создавали спутников без ресурса маны. В новой боевой
                    // системе мана есть у каждого участника группы, даже у немагических классов.
                    foreach (var member in state.Party.Where(p => p.ManaMax <= 0))
                    {
                        member.ManaMax = 10;
                        member.ManaCurrent = 10;
                    }
                    // Перенос старого списочного инвентаря спутников в ту же сетку 11×5,
                    // которой пользуются новые сохранения и запросы передачи вещей.
                    foreach (var member in state.Party)
                    {
                        foreach (var old in member.Inventory.ToList())
                        {
                            if (member.Grid.Any(i => i.Name.Equals(old.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                member.Inventory.Remove(old);
                                continue;
                            }
                            var def = _catalog.Resolve(null, old.Name);
                            var spot = InventoryOps.FindFree(member.Grid, def.W, def.H);
                            if (spot is null) continue;
                            member.Grid.Add(new GridItem
                            {
                                Name = old.Name, Quantity = Math.Max(1, old.Quantity), Note = old.Note,
                                Icon = def.Id, W = def.W, H = def.H, Slot = def.EquipSlot, TwoHanded = def.TwoHanded,
                                Consumable = def.Cat is "potion" or "food" or "scroll" or "book",
                                Col = spot.Value.Col, Row = spot.Value.Row,
                            });
                            member.Inventory.Remove(old);
                        }
                    }
                    return state;
                }
            }
            catch
            {
                // повреждённый файл — начинаем с пустого состояния
            }
        }

        // Новая кампания рождается в текущем сеттинге; старые сохранения без поля остаются фэнтези.
        return new RpgState { GenreId = Genre.Current };
    }

    private void OnChatsChanged()
    {
        // Зачищаем RPG-заметки чатов, которые были удалены.
        try
        {
            var dir = RpgDirectory;
            var chatsDir = _chats.StorageDirectory;
            if (dir is null || chatsDir is null || !Directory.Exists(dir))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                if (!File.Exists(Path.Combine(chatsDir, id + ".json")))
                {
                    Delete(id);
                }
            }
        }
        catch
        {
            // зачистка — не критичный путь
        }
    }

    private void OnSettingsSaved(HarnessSettings _)
    {
        lock (_lock)
        {
            _cache.Clear();
            _scaffolded.Clear();
            _writtenNpcFiles.Clear();
            _writtenPartyFiles.Clear();
            _lastHeroMd.Clear();
        }
    }

    /// <summary>Шаблоны файлов, которые дальше ведёт мастер (только отсутствующие).</summary>
    private static void WriteMasterTemplates(string root)
    {
        foreach (var (file, content) in CampaignTemplates.MasterFiles)
        {
            WriteIfMissing(Path.Combine(root, file), content);
        }
    }

    private static void WriteIfMissing(string path, string content)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, content);
        }
    }

    /// <summary>
    /// Если Hero.md изменился вне панели (человек правил руками) — переносит известные поля
    /// (имя, раса, класс, уровень, ХП, мана, золото, опыт, статы, состояния, заметки) в состояние.
    /// Сравнение идёт с последним содержимым, записанным нами: собственные записи не импортируются.
    /// </summary>
    private void ImportHeroMdIfChanged(string sessionId, string path, CharacterSheet c)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var current = File.ReadAllText(path);
            if (_lastHeroMd.TryGetValue(sessionId, out var last) && last == current)
            {
                return; // файл не менялся с нашей последней записи
            }

            TryParseHeroMd(current, c);
        }
        catch
        {
            // не смогли прочитать — оставляем состояние как есть
        }
    }

    /// <summary>Английские подписи полей Hero.md (лист, записанный в английском интерфейсе) → русские ключи разбора.</summary>
    private static readonly Dictionary<string, string> HeroMdLabelsEn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Race"] = "Раса", ["Class"] = "Класс", ["Gender"] = "Пол", ["Portrait"] = "Портрет", ["Level"] = "Уровень",
        ["HP"] = "ХП", ["Mana"] = "Мана", ["Gold"] = "Золото", ["XP"] = "Опыт", ["Stats"] = "Характеристики", ["Conditions"] = "Состояния",
    };

    /// <summary>Разбирает известные поля из Hero.md; возвращает true, если нашёл хотя бы одно.</summary>
    internal static bool TryParseHeroMd(string md, CharacterSheet c)
    {
        var found = false;
        var inNotes = false;
        var notes = new List<string>();

        foreach (var rawLine in md.Split('\n'))
        {
            var line = rawLine.TrimEnd();

            if (line.StartsWith("## "))
            {
                inNotes = line.TrimStart('#').Trim() is var h && (h.Equals("Заметки", StringComparison.OrdinalIgnoreCase) || h.Equals("Notes", StringComparison.OrdinalIgnoreCase));
                continue;
            }

            if (inNotes)
            {
                if (line.Length > 0)
                {
                    notes.Add(line);
                }

                continue;
            }

            if (line.StartsWith("# "))
            {
                var title = line[2..].Trim();
                if (title.Length > 0 && !title.Equals("Герой", StringComparison.OrdinalIgnoreCase) && !title.Equals("Hero", StringComparison.OrdinalIgnoreCase))
                {
                    c.Name = title;
                    found = true;
                }

                continue;
            }

            var m = System.Text.RegularExpressions.Regex.Match(line, @"^\s*-\s*\*\*(.+?):\*\*\s*(.*)$");
            if (!m.Success)
            {
                continue;
            }

            var label = m.Groups[1].Value.Trim();
            label = HeroMdLabelsEn.GetValueOrDefault(label, label);
            var value = m.Groups[2].Value.Trim();
            switch (label)
            {
                case "Раса":
                    c.Race = value;
                    found = true;
                    break;
                case "Класс":
                    c.CharClass = value;
                    found = true;
                    break;
                case "Пол":
                    c.Gender = value;
                    found = true;
                    break;
                case "Портрет":
                    c.Avatar = value;
                    found = true;
                    break;
                case "Уровень":
                    c.Level = value;
                    found = true;
                    break;
                case "ХП":
                    var hp = value.Split('/');
                    if (hp.Length == 2 && int.TryParse(hp[0].Trim(), out var hc) && int.TryParse(hp[1].Trim(), out var hm))
                    {
                        c.HpCurrent = hc;
                        c.HpMax = hm;
                        found = true;
                    }
                    break;
                case "Мана":
                    var mp = value.Split('/');
                    if (mp.Length == 2 && int.TryParse(mp[0].Trim(), out var mc) && int.TryParse(mp[1].Trim(), out var mm))
                    {
                        c.ManaCurrent = mc;
                        c.ManaMax = mm;
                        found = true;
                    }
                    break;
                case "Золото":
                    if (int.TryParse(value, out var gold))
                    {
                        c.Gold = gold;
                        found = true;
                    }
                    break;
                case "Опыт":
                    if (int.TryParse(value, out var xp))
                    {
                        c.Xp = xp;
                        found = true;
                    }
                    break;
                case "Характеристики":
                    foreach (var (key, prop) in new[]
                             {
                                 ("СИЛ", "Str"), ("ЛОВ", "Dex"), ("ТЕЛ", "Con"),
                                 ("ИНТ", "Int"), ("МДР", "Wis"), ("ХАР", "Cha"),
                             })
                    {
                        var sm = System.Text.RegularExpressions.Regex.Match(value, "(?:" + key + "|" + prop.ToUpperInvariant() + @")\s+(\d+)");
                        if (sm.Success && int.TryParse(sm.Groups[1].Value, out var sv))
                        {
                            typeof(CharacterSheet).GetProperty(prop)!.SetValue(c, sv);
                            found = true;
                        }
                    }
                    break;
                case "Состояния":
                    c.Status.Clear();
                    foreach (var part in value.Split(','))
                    {
                        var st = part.Trim();
                        if (st.Length > 0)
                        {
                            c.Status.Add(st);
                        }
                    }

                    found = true;
                    break;
            }
        }

        if (notes.Count > 0)
        {
            c.Notes = string.Join("\n", notes).Trim();
            found = true;
        }

        return found;
    }

    // ===== Markdown-представление =====

    /// <summary>Короткое название характеристики (СИЛ/STR) на языке интерфейса.</summary>
    private static string S(int i) => DndStatNames.Short[i];

    internal static string BuildHeroMd(CharacterSheet c)
    {
        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(string.IsNullOrWhiteSpace(c.Name) ? Lang.T("Герой", "Hero") : c.Name!.Trim());
        sb.AppendLine();
        sb.AppendLine(Lang.T("> Лист героя — зеркало панели «Книга героя». Правится инструментами мастера или руками (поля «- **Поле:** значение» переносятся в панель).", "> Hero sheet — a mirror of the \"Hero's Book\" panel. Edited by the GM's tools or by hand (fields \"- **Field:** value\" carry over to the panel)."));

        var lines = new List<(string Label, string Value)>();
        if (!string.IsNullOrWhiteSpace(c.Race))
        {
            lines.Add((Lang.T("Раса", "Race"), c.Race.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(c.CharClass))
        {
            lines.Add((Lang.T("Класс", "Class"), c.CharClass.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(c.Gender))
        {
            lines.Add((Lang.T("Пол", "Gender"), c.Gender.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(c.Avatar))
        {
            lines.Add((Lang.T("Портрет", "Portrait"), c.Avatar.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(c.Level))
        {
            lines.Add((Lang.T("Уровень", "Level"), c.Level.Trim()));
        }

        if (c.HpMax > 0)
        {
            lines.Add((Lang.T("ХП", "HP"), $"{c.HpCurrent}/{c.HpMax}"));
        }
        if (c.HpCurrent <= 0 && c.DeathState.Length > 0)
        {
            lines.Add((Lang.T("При смерти", "Dying"), Lang.T($"{c.DeathState}; успехи {c.DeathSaveSuccesses}/3, провалы {c.DeathSaveFailures}/3", $"{c.DeathState}; successes {c.DeathSaveSuccesses}/3, failures {c.DeathSaveFailures}/3")));
        }

        if (c.ManaMax > 0)
        {
            lines.Add((Lang.T("Мана", "Mana"), $"{c.ManaCurrent}/{c.ManaMax}"));
        }
        if (c.Shield > 0) lines.Add((Lang.T("Щит", "Shield"), c.Shield.ToString()));
        lines.Add((Genre.PickT("Гильдия приключенцев", "Репутация у фиксеров", "Репутация в узких кругах", "Adventurers' Guild", "Fixer reputation", "Reputation in certain circles"), $"{GuildRanks.TitleFor(c.GuildReputation)}, {Lang.T("репутация", "reputation")} {c.GuildReputation}"));
        if (c.Skills.Count > 0) lines.Add((Lang.T("Навыки", "Skills"), string.Join("; ", c.Skills.Select(s=>$"{s.Name} {Lang.T("ур.", "tier ")}{s.Rank} [{s.Category}] — {s.Description}"))));

        if (c.Gold > 0)
        {
            lines.Add((Lang.T("Золото", "Gold"), c.Gold.ToString()));
        }

        if (c.Xp > 0)
        {
            lines.Add((Lang.T("Опыт", "XP"), c.Xp.ToString()));
        }

        lines.Add((Lang.T("Характеристики", "Stats"),
            $"{S(0)} {c.Str} ({DndStatNames.ModText(c.Str)}), {S(1)} {c.Dex} ({DndStatNames.ModText(c.Dex)}), " +
            $"{S(2)} {c.Con} ({DndStatNames.ModText(c.Con)}), {S(3)} {c.Int} ({DndStatNames.ModText(c.Int)}), " +
            $"{S(4)} {c.Wis} ({DndStatNames.ModText(c.Wis)}), {S(5)} {c.Cha} ({DndStatNames.ModText(c.Cha)})"));

        var itemBonus = ItemStats.BonusText(ItemStats.Totals(c));
        if (itemBonus.Length > 0)
        {
            lines.Add((Lang.T("Бонусы вещей", "Item bonuses"), itemBonus));
        }

        lines.Add((Lang.T("Класс брони", "Armor class"), ItemStats.ArmorClass(c).ToString()));
        lines.Add((Lang.T("Атака", "Attack"), ItemStats.AttackLine(c)));
        var effects = ItemStats.ActiveEffects(c);
        if (effects.Count > 0)
        {
            lines.Add((Lang.T("Свойства вещей", "Item properties"), string.Join("; ", effects.Select(e => $"{e.Effect} ({e.Item})"))));
        }

        if (c.Status.Count > 0)
        {
            lines.Add((Lang.T("Состояния", "Conditions"), string.Join(", ", c.Status)));
        }

        if (lines.Count > 0)
        {
            sb.AppendLine();
            foreach (var (label, value) in lines)
            {
                sb.Append("- **").Append(label).Append(":** ").AppendLine(value);
            }
        }

        if (!string.IsNullOrWhiteSpace(c.Notes))
        {
            sb.AppendLine().AppendLine(Lang.T("## Заметки", "## Notes")).AppendLine().AppendLine(c.Notes!.Trim());
        }

        return sb.ToString();
    }

    /// <summary>Инвентарь для файлов кампании: надетое + содержимое сумки-сетки.</summary>
    internal static string BuildInventoryMd(RpgState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Lang.T("# Инвентарь", "# Inventory"));
        sb.AppendLine();
        sb.AppendLine(Lang.T("> Зеркало панели «Книга героя». Меняется инструментами update_grid_item / equip_item — ручные правки файла перезапишутся.", "> A mirror of the \"Hero's Book\" panel. Changed by the update_grid_item / equip_item tools — manual edits to this file will be overwritten."));

        var worn = ItemStats.Worn(state.Character)
            .Where(w => !string.IsNullOrWhiteSpace(w.Item.Name))
            .Select(w => (Slots: w.Item.TwoHanded ? "Hand1+Hand2" : w.Slot, w.Item))
            .ToList();
        sb.AppendLine().AppendLine(Lang.T("## Надето", "## Equipped")).AppendLine();
        if (worn.Count == 0)
        {
            sb.AppendLine(Lang.T("_Ничего._", "_Nothing._"));
        }
        else
        {
            foreach (var (slots, item) in worn)
            {
                sb.Append("- **").Append(slots).Append(":** ").Append(item.Name.Trim());
                if (ItemStats.HasStats(item))
                {
                    sb.Append(" `").Append(ItemStats.Describe(item)).Append('`');
                }

                if (!string.IsNullOrWhiteSpace(item.Note))
                {
                    sb.Append(" — ").Append(item.Note.Trim());
                }

                sb.AppendLine();
            }
        }

        var bag = state.Grid.Where(i => !string.IsNullOrWhiteSpace(i.Name)).ToList();
        sb.AppendLine().AppendLine(Lang.T("## Сумка", "## Bag")).AppendLine();
        if (bag.Count == 0)
        {
            sb.AppendLine(Lang.T("_Пусто._", "_Empty._"));
            return sb.ToString();
        }

        sb.AppendLine(Lang.T("| Предмет | Кол-во | Размер | Характеристики | Заметка |", "| Item | Qty | Size | Stats | Note |"));
        sb.AppendLine("| --- | ---: | :---: | --- | --- |");
        foreach (var item in bag)
        {
            sb.Append("| ").Append(Cell(item.Name))
              .Append(" | ").Append(Math.Max(item.Quantity, 1))
              .Append(" | ").Append(item.W).Append('x').Append(item.H)
              .Append(" | ").Append(Cell(ItemStats.Describe(item)))
              .Append(" | ").Append(Cell(item.Note)).AppendLine(" |");
        }

        sb.AppendLine().AppendLine(Lang.T($"Занято клеток: {bag.Sum(i => i.W * i.H)} из {RpgState.GridCols * RpgState.GridRows}.", $"Cells used: {bag.Sum(i => i.W * i.H)} of {RpgState.GridCols * RpgState.GridRows}."));
        return sb.ToString();
    }

    internal static string BuildNpcMd(ImportantNpc npc, ItemEconomy? economy = null)
    {
        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(string.IsNullOrWhiteSpace(npc.Name) ? Lang.T("Без имени", "Unnamed") : npc.Name!.Trim());
        sb.AppendLine();
        sb.AppendLine(Lang.T("> Зеркало книги героя: ведётся инструментами update_npc / set_merchant / update_shop_item — ручные правки перезапишутся.", "> A mirror of the Hero's Book: maintained by the update_npc / set_merchant / update_shop_item tools — manual edits will be overwritten."));
        sb.AppendLine();

        var lines = new List<(string, string)>();
        if (!string.IsNullOrWhiteSpace(npc.Role))
        {
            lines.Add((Lang.T("Роль", "Role"), npc.Role.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(npc.Location))
        {
            lines.Add((Lang.T("Где найти", "Where to find"), npc.Location.Trim()));
        }

        lines.Add((Lang.T("Категория", "Category"), npc.Category == "nearby" ? Lang.T("взаимодействие неподалёку", "nearby interaction") : Lang.T("важный персонаж", "important character")));

        if (!string.IsNullOrWhiteSpace(npc.Attitude))
        {
            lines.Add((Lang.T("Отношение", "Attitude"), npc.Attitude.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(npc.Level))
        {
            lines.Add((Lang.T("Уровень/мощь", "Level/power"), npc.Level.Trim()));
        }

        if (npc.HpMax > 0)
        {
            lines.Add((Lang.T("ХП", "HP"), $"{npc.HpCurrent}/{npc.HpMax}"));
        }

        foreach (var (label, value) in lines)
        {
            sb.Append("- **").Append(label).Append(":** ").AppendLine(value);
        }

        if (!string.IsNullOrWhiteSpace(npc.Abilities))
        {
            sb.AppendLine().AppendLine(Lang.T("## Способности", "## Abilities")).AppendLine().AppendLine(npc.Abilities!.Trim());
        }

        if (!string.IsNullOrWhiteSpace(npc.Note))
        {
            sb.AppendLine().AppendLine(Lang.T("## О персонаже", "## About")).AppendLine().AppendLine(npc.Note!.Trim());
        }

        if (npc.Shop is { } shop)
        {
            AppendShopMd(sb, shop, economy);
        }

        return sb.ToString();
    }

    private static void AppendShopMd(StringBuilder sb, MerchantShop shop, ItemEconomy? economy)
    {
        var kind = ItemEconomy.Merchant(shop.Kind);
        var main = shop.Buys.Count > 0 ? shop.Buys.ToArray() : kind.Main;
        string Cats(IEnumerable<string> keys) =>
            string.Join(", ", keys.Select(k => ItemEconomy.Category(k)?.Title.ToLowerInvariant() ?? k));

        sb.AppendLine().AppendLine(Lang.T("## Лавка", "## Shop")).AppendLine();
        if (!string.IsNullOrWhiteSpace(shop.Title))
        {
            sb.Append(Lang.T("- **Вывеска:** ", "- **Sign:** ")).AppendLine(shop.Title.Trim());
        }

        sb.Append(Lang.T("- **Тип:** ", "- **Type:** ")).Append(kind.Title).Append(" (").Append(shop.Kind).Append(Lang.T("), поселение: ", "), settlement: ")).AppendLine(shop.Tier);
        sb.Append(Lang.T("- **Деньги торговца:** ", "- **Merchant's money:** ")).Append(shop.Gold).AppendLine(Lang.T(" з.", " g."));
        sb.Append(Lang.T("- **Наценка:** ×", "- **Markup:** ×")).Append(shop.Markup.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))
          .Append(Lang.T("; скупает своё за ", "; buys own goods for ")).Append((int)Math.Round(shop.BuyRate * 100)).Append(Lang.T("% цены", "% of the price"))
          .AppendLine(shop.OffRate > 0 ? Lang.T($", смежное — за {(int)Math.Round(shop.OffRate * 100)}%", $", related goods — for {(int)Math.Round(shop.OffRate * 100)}%") : Lang.T(", смежное не берёт", ", doesn't take related goods"));
        sb.Append(Lang.T("- **Торгует:** ", "- **Sells:** ")).AppendLine(Cats(main));
        var also = (shop.Also.Count > 0 ? shop.Also.ToArray() : kind.Also).Except(main).ToArray();
        if (also.Length > 0 && shop.OffRate > 0)
        {
            sb.Append(Lang.T("- **Берёт задёшево:** ", "- **Takes cheaply:** ")).AppendLine(Cats(also));
        }

        sb.AppendLine();
        if (shop.Items.Count == 0)
        {
            sb.AppendLine(Lang.T("_Прилавок пуст._", "_The counter is empty._"));
            return;
        }

        sb.AppendLine(Lang.T("| Товар | Кол-во | Цена | Редкость | Характеристики | Заметка |", "| Goods | Qty | Price | Rarity | Stats | Note |"));
        sb.AppendLine("| --- | ---: | ---: | --- | --- | --- |");
        foreach (var item in shop.Items)
        {
            var price = economy is null ? item.Value : (int)Math.Ceiling(economy.UnitValue(item) * shop.Markup);
            sb.Append("| ").Append(Cell(item.Name))
              .Append(" | ").Append(Math.Max(item.Quantity, 1))
              .Append(" | ").Append(price).Append(Lang.T(" з.", " g."))
              .Append(" | ").Append(ItemEconomy.RarityTitle(item.Rarity))
              .Append(" | ").Append(Cell(ItemStats.Describe(item)))
              .Append(" | ").Append(Cell(item.Note)).AppendLine(" |");
        }
    }

    internal static string BuildAdversariesMd(List<Adversary> adversaries)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Lang.T("# Противники", "# Adversaries"));
        sb.AppendLine();
        sb.AppendLine(Lang.T("> Зеркало книги героя (update_adversary). Роль в истории обязательна: ordinary — рядовой, elite — элита, quest_boss — босс задания, dungeon_boss — босс данжа, arc_boss — босс сюжетной арки.", "> A mirror of the Hero's Book (update_adversary). The story role is required: ordinary — a rank-and-file foe, elite — elite, quest_boss — quest boss, dungeon_boss — dungeon boss, arc_boss — story arc boss."));

        var real = adversaries.Where(a => !string.IsNullOrWhiteSpace(a.Name)).ToList();
        if (real.Count == 0)
        {
            sb.AppendLine().AppendLine(Lang.T("_Активных противников нет._", "_No active adversaries._"));
            return sb.ToString();
        }

        foreach (var a in real)
        {
            sb.AppendLine();
            sb.Append("## ").AppendLine(a.Name.Trim());
            sb.Append(Lang.T("- **Роль в истории:** ", "- **Story role:** ")).AppendLine((a.ThreatTier ?? "ordinary") switch
            {
                "minion" => Lang.T("Прислужник (minion)", "Minion (minion)"),
                "elite" => Lang.T("Элита (elite)", "Elite (elite)"),
                "quest_boss" => Lang.T("Босс задания (quest_boss)", "Quest boss (quest_boss)"),
                "dungeon_boss" => Lang.T("Босс данжа (dungeon_boss)", "Dungeon boss (dungeon_boss)"),
                "arc_boss" => Lang.T("Босс сюжетной арки (arc_boss)", "Story arc boss (arc_boss)"),
                _ => Lang.T("Рядовой противник (ordinary)", "Rank-and-file adversary (ordinary)")
            });
            if (!string.IsNullOrWhiteSpace(a.Portrait))
            {
                sb.Append(Lang.T("- **Портрет:** ", "- **Portrait:** ")).AppendLine(a.Portrait.Trim());
            }
            if (!string.IsNullOrWhiteSpace(a.Level))
            {
                sb.Append(Lang.T("- **Уровень/CR:** ", "- **Level/CR:** ")).AppendLine(a.Level.Trim());
            }

            if (a.HpMax > 0)
            {
                sb.Append(Lang.T("- **ХП:** ", "- **HP:** ")).Append(a.HpCurrent).Append('/').AppendLine(a.HpMax.ToString());
            }

            if (a.Morale.Length > 0)
            {
                sb.Append(Lang.T("- **Вне боя:** ", "- **Out of the fight:** ")).AppendLine(a.Morale == "fled" ? Lang.T("бежал", "fled") : Lang.T("сдался", "surrendered"));
            }

            if (a.Ac > 0)
            {
                sb.Append(Lang.T("- **Бой:** КБ ", "- **Combat:** AC ")).Append(a.Ac).Append(Lang.T(", атака ", ", attack ")).Append(a.AttackBonus.ToString("+#;-#;+0"))
                  .Append(a.Attacks > 1 ? $" ×{a.Attacks}" : "").Append(Lang.T(", урон ", ", damage ")).Append(a.Damage).Append(", DC ").Append(a.SaveDc)
                  .Append(a.StaggerMax > 0 ? Lang.T($", стойкость {a.Stagger}/{a.StaggerMax}", $", poise {a.Stagger}/{a.StaggerMax}") : "")
                  .AppendLine(a.Legendary > 0 ? Lang.T($", легендарных действий {a.Legendary}", $", legendary actions {a.Legendary}") : "");
            }

            if (!string.IsNullOrWhiteSpace(a.Status))
            {
                sb.Append(Lang.T("- **Состояние:** ", "- **Condition:** ")).AppendLine(a.Status.Trim());
            }

            if (!string.IsNullOrWhiteSpace(a.Abilities))
            {
                sb.Append(Lang.T("- **Способности и тактика:** ", "- **Abilities and tactics:** ")).AppendLine(a.Abilities.Trim());
            }

            if (!string.IsNullOrWhiteSpace(a.Notes))
            {
                sb.Append(Lang.T("- **Заметки:** ", "- **Notes:** ")).AppendLine(a.Notes.Trim());
            }
        }

        return sb.ToString();
    }

    internal static string BuildCitiesMd(List<CityRecord> cities)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Lang.T("# Города и поселения", "# Cities and settlements"));
        sb.AppendLine();
        sb.AppendLine(Lang.T("> Зеркало книги героя (update_city). Раздел на поселение: описание и список мест «**Название** (тип) — заметка».", "> A mirror of the Hero's Book (update_city). A section per settlement: a description and a list of places \"**Name** (type) — note\"."));

        var real = cities.Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList();
        if (real.Count == 0)
        {
            sb.AppendLine().AppendLine(Lang.T("_Города и поселения пока не описаны._", "_No cities or settlements described yet._"));
            return sb.ToString();
        }

        foreach (var c in real)
        {
            sb.AppendLine();
            sb.Append("## ").AppendLine(c.Name.Trim());
            if (!string.IsNullOrWhiteSpace(c.Description))
            {
                sb.AppendLine().AppendLine(c.Description.Trim());
            }

            foreach (var p in c.Points.Where(p => !string.IsNullOrWhiteSpace(p.Name)))
            {
                sb.Append("- **").Append(p.Name.Trim());
                if (!string.IsNullOrWhiteSpace(p.Type))
                {
                    sb.Append("** (").Append(p.Type.Trim()).Append(')');
                }
                else
                {
                    sb.Append("**");
                }

                if (!string.IsNullOrWhiteSpace(p.Note))
                {
                    sb.Append(" — ").Append(p.Note.Trim());
                }

                sb.AppendLine();
            }

            if (c.Acquaintances.Count > 0)
            {
                sb.AppendLine().AppendLine(Lang.T("### Знакомые местные", "### Local acquaintances"));
                foreach (var a in c.Acquaintances.Where(a => !string.IsNullOrWhiteSpace(a.Name)))
                {
                    sb.Append("- **").Append(a.Name.Trim()).Append("**")
                      .Append(string.IsNullOrWhiteSpace(a.Role) ? "" : $" — {a.Role.Trim()}")
                      .Append(string.IsNullOrWhiteSpace(a.Note) ? "" : $": {a.Note.Trim()}").AppendLine();
                }
            }
            if (c.AdventureGuild is { } guild)
            {
                sb.AppendLine().Append("### ").AppendLine(guild.Name);
                foreach (var j in guild.Jobs) sb.Append("- **").Append(j.Name).Append("** [").Append(j.State).Append(Lang.T(", ранг ", ", rank ")).Append(j.MinRank).Append("] — ").Append(j.Description).Append(Lang.T("; награда: ", "; reward: ")).Append(j.GoldReward).Append(Lang.T(" золота, +", " gold, +")).Append(j.ReputationReward).Append(Lang.T(" реп.", " rep.")).Append(string.IsNullOrWhiteSpace(j.ItemReward)?"":$", {j.ItemReward}").AppendLine();
            }
        }

        return sb.ToString();
    }

    internal static string BuildQuestsMd(List<QuestRecord> quests)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Lang.T("# Задания", "# Quests"));
        sb.AppendLine();
        sb.AppendLine(Lang.T("> Зеркало книги героя (update_quest). Раздел на задание: описание, Кто выдал, Награда, Ход выполнения.", "> A mirror of the Hero's Book (update_quest). A section per quest: description, Giver, Reward, Progress."));

        var real = quests.Where(q => !string.IsNullOrWhiteSpace(q.Name)).ToList();
        if (real.Count == 0)
        {
            sb.AppendLine().AppendLine(Lang.T("_Принятых заданий нет._", "_No accepted quests._"));
            return sb.ToString();
        }

        foreach (var q in real)
        {
            sb.AppendLine().Append("## ").Append(q.Name.Trim());
            sb.AppendLine(q.State switch
            {
                "done" => Lang.T(" — ✅ выполнено", " — ✅ completed"),
                "failed" => Lang.T(" — ❌ провалено", " — ❌ failed"),
                _ => "",
            });

            if (!string.IsNullOrWhiteSpace(q.Description))
            {
                sb.AppendLine().AppendLine(q.Description.Trim());
            }

            if (!string.IsNullOrWhiteSpace(q.Giver))
            {
                sb.Append(Lang.T("- **Кто выдал:** ", "- **Giver:** ")).AppendLine(q.Giver.Trim());
            }

            if (!string.IsNullOrWhiteSpace(q.Reward))
            {
                sb.Append(Lang.T("- **Награда:** ", "- **Reward:** ")).AppendLine(q.Reward.Trim());
            }

            if (q.Progress.Count > 0)
            {
                sb.AppendLine(Lang.T("- **Ход выполнения:**", "- **Progress:**"));
                foreach (var step in q.Progress)
                {
                    sb.Append("  - ").AppendLine(step);
                }
            }
        }

        return sb.ToString();
    }

    internal static string BuildStoryArcMd(List<StoryArc> arcs)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Lang.T("# Сюжетные арки", "# Story arcs"));
        sb.AppendLine();
        sb.AppendLine(Lang.T("> Зеркало книги героя (update_arc). Здесь только известная игроку завязка и журнал уже произошедших событий; планы и тайны мастера сюда не попадают.", "> A mirror of the Hero's Book (update_arc). This contains only the player-known setup and a log of events that have already happened; the GM's plans and secrets are not included."));

        var real = arcs.Where(a => !string.IsNullOrWhiteSpace(a.Name)).ToList();
        if (real.Count == 0)
        {
            sb.AppendLine().AppendLine(Lang.T("_Арки пока не намечены._", "_No arcs planned yet._"));
        }

        // Активная арка — первой.
        foreach (var arc in real.OrderByDescending(a => a.State.Equals("active", StringComparison.OrdinalIgnoreCase)))
        {
            sb.AppendLine().Append("## ").Append(arc.Name.Trim());
            sb.AppendLine(arc.State switch
            {
                "active" => Lang.T(" — 🎯 активная", " — 🎯 active"),
                "done" => Lang.T(" — ✅ завершена", " — ✅ completed"),
                "abandoned" => Lang.T(" — 🚫 оставлена", " — 🚫 abandoned"),
                _ => "",
            });

            if (!string.IsNullOrWhiteSpace(arc.Premise))
            {
                sb.AppendLine().AppendLine(arc.Premise.Trim());
            }

            if (arc.Events.Count > 0)
            {
                sb.AppendLine().AppendLine(Lang.T("### Что произошло", "### What happened")).AppendLine();
                for (var i = 0; i < arc.Events.Count; i++)
                {
                    sb.Append(i + 1).Append(". ").AppendLine(arc.Events[i]);
                }
            }
        }

        return sb.ToString();
    }

    internal static string BuildNotablesMd(List<ImportantNpc> notables)
    {
        var sb = new StringBuilder(Lang.T("# Персонажи, которых нельзя забыть\n\n", "# Characters not to forget\n\n"));
        sb.AppendLine(Lang.T("> Долги, обещания, связанные задания, будущие вопросы и другие незакрытые связи (update_npc category=notable).", "> Debts, promises, linked quests, future questions and other open ties (update_npc category=notable)."));
        foreach (var npc in notables.Where(n => !string.IsNullOrWhiteSpace(n.Name)))
        {
            sb.Append("\n## ").AppendLine(npc.Name.Trim());
            if (!string.IsNullOrWhiteSpace(npc.Role)) sb.Append(Lang.T("- **Роль:** ", "- **Role:** ")).AppendLine(npc.Role.Trim());
            if (!string.IsNullOrWhiteSpace(npc.Location)) sb.Append(Lang.T("- **Где найти:** ", "- **Where to find:** ")).AppendLine(npc.Location.Trim());
            if (!string.IsNullOrWhiteSpace(npc.Attitude)) sb.Append(Lang.T("- **Отношение:** ", "- **Attitude:** ")).AppendLine(npc.Attitude.Trim());
            if (!string.IsNullOrWhiteSpace(npc.Note)) sb.Append(Lang.T("- **Почему нельзя забыть:** ", "- **Why not to forget:** ")).AppendLine(npc.Note.Trim());
        }
        if (!notables.Any(n => !string.IsNullOrWhiteSpace(n.Name))) sb.AppendLine(Lang.T("\n_Пока никого._", "\n_Nobody yet._"));
        return sb.ToString();
    }

    internal static string BuildPartyMemberMd(PartyMember p)
    {
        var sb = new StringBuilder($"# {p.Name}\n\n");
        sb.AppendLine(Lang.T("> Самостоятельный участник группы (update_party_member / update_party_inventory). Инвентарь меняется только с его явного согласия.", "> An independent party member (update_party_member / update_party_inventory). The inventory changes only with their explicit consent."));
        sb.Append(Lang.T("- **Раса / класс / пол:** ", "- **Race / class / gender:** ")).Append(p.Race).Append(" / ").Append(p.CharClass).Append(" / ").AppendLine(p.Gender);
        sb.Append(Lang.T("- **Уровень:** ", "- **Level:** ")).AppendLine(p.Level);
        sb.Append(Lang.T("- **Роль в группе:** ", "- **Party role:** ")).AppendLine(Progression.ArchetypeOf(p).Title);
        var atk = ItemStats.BestAttack(p);
        sb.Append(Lang.T("- **Бой:** КБ ", "- **Combat:** AC ")).Append(ItemStats.ArmorClass(p)).Append(", ").Append(atk.Title).Append(Lang.T(": атака ", ": attack "))
          .Append(atk.Attack.ToString("+#;-#;+0")).Append(Lang.T(", урон ", ", damage ")).AppendLine(atk.DamageText);
        var gear = ItemStats.Worn(p).Select(w => w.Item.Name + (ItemStats.HasStats(w.Item) ? $" ({ItemStats.Describe(w.Item)})" : "")).ToList();
        if (gear.Count > 0) sb.Append(Lang.T("- **Снаряжение:** ", "- **Gear:** ")).AppendLine(string.Join("; ", gear));
        sb.Append(Lang.T("- **ХП:** ", "- **HP:** ")).Append(p.HpCurrent).Append('/').AppendLine(p.HpMax.ToString());
        if (p.HpCurrent <= 0 && p.DeathState.Length > 0)
            sb.Append(Lang.T("- **При смерти:** ", "- **Dying:** ")).Append(p.DeathState).Append(Lang.T("; успехи ", "; successes ")).Append(p.DeathSaveSuccesses)
              .Append(Lang.T("/3, провалы ", "/3, failures ")).Append(p.DeathSaveFailures).AppendLine("/3");
        sb.Append(Lang.T("- **Мана:** ", "- **Mana:** ")).Append(p.ManaCurrent).Append('/').AppendLine(p.ManaMax.ToString());
        sb.Append(Lang.T("- **Щит:** ", "- **Shield:** ")).AppendLine(p.Shield.ToString());
        sb.Append(Lang.T("- **Портрет:** ", "- **Portrait:** ")).AppendLine(p.Portrait);
        sb.Append(Lang.T("- **Характеристики:** ", "- **Stats:** ")).Append(S(0)).Append(' ').Append(p.Str).Append(", ").Append(S(1)).Append(' ').Append(p.Dex).Append(", ").Append(S(2)).Append(' ').Append(p.Con)
          .Append(", ").Append(S(3)).Append(' ').Append(p.Int).Append(", ").Append(S(4)).Append(' ').Append(p.Wis).Append(", ").Append(S(5)).Append(' ').AppendLine(p.Cha.ToString());
        sb.Append(Lang.T("- **Навыки:** ", "- **Skills:** ")).AppendLine(p.LearnedSkills.Count == 0 ? "—" : string.Join("; ", p.LearnedSkills.Select(s=>$"{s.Name} {Lang.T("ур.", "tier ")}{s.Rank} [{s.Category}] — {s.Description}")));
        if (!string.IsNullOrWhiteSpace(p.Note)) sb.Append(Lang.T("- **О персонаже:** ", "- **About:** ")).AppendLine(p.Note.Trim());
        sb.AppendLine(Lang.T("\n## Инвентарь", "\n## Inventory"));
        foreach (var item in p.Grid) sb.Append("- ").Append(item.Name).Append(item.Quantity > 1 ? $" ×{item.Quantity}" : "").Append(string.IsNullOrWhiteSpace(item.Note) ? "" : $" — {item.Note}").AppendLine();
        foreach (var item in p.Inventory) sb.Append("- ").Append(item.Name).Append(item.Quantity > 1 ? $" ×{item.Quantity}" : "").Append(string.IsNullOrWhiteSpace(item.Note) ? "" : $" — {item.Note}").AppendLine();
        if (p.Grid.Count+p.Inventory.Count == 0) sb.AppendLine(Lang.T("_Пусто._", "_Empty._"));
        return sb.ToString();
    }

    internal static string BuildCandidatesMd(IEnumerable<CompanionCandidate> candidates)
    {
        var list = candidates.Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList();
        var sb = new StringBuilder(Lang.T("# Возможные спутники\n\n", "# Possible companions\n\n"));
        sb.AppendLine(Lang.T("> Зеркало пути знакомства (update_companion_candidate). Кандидат не является членом группы до recruit_companion.", "> A mirror of the acquaintance path (update_companion_candidate). A candidate is not a party member until recruit_companion."));
        foreach (var c in list)
        {
            sb.Append("\n## ").AppendLine(c.Name.Trim());
            sb.Append(Lang.T("- **Этап:** ", "- **Stage:** ")).AppendLine(c.Stage);
            sb.Append(Lang.T("- **Источник:** ", "- **Source:** ")).Append(c.Source).Append(string.IsNullOrWhiteSpace(c.Arc) ? "" : $" · {c.Arc}").AppendLine();
            if (!string.IsNullOrWhiteSpace(c.Concept)) sb.Append(Lang.T("- **Концепт:** ", "- **Concept:** ")).AppendLine(c.Concept.Trim());
            if (!string.IsNullOrWhiteSpace(c.Motivation)) sb.Append(Lang.T("- **Мотив:** ", "- **Motive:** ")).AppendLine(c.Motivation.Trim());
            if (!string.IsNullOrWhiteSpace(c.PersonalGoal)) sb.Append(Lang.T("- **Личная цель:** ", "- **Personal goal:** ")).AppendLine(c.PersonalGoal.Trim());
            if (!string.IsNullOrWhiteSpace(c.JoinCondition)) sb.Append(Lang.T("- **Условие вступления:** ", "- **Joining condition:** ")).AppendLine(c.JoinCondition.Trim());
            if (!string.IsNullOrWhiteSpace(c.PlayerHint)) sb.Append(Lang.T("- **Видимая зацепка:** ", "- **Visible hook:** ")).AppendLine(c.PlayerHint.Trim());
            sb.Append(Lang.T("- **Совместных испытаний:** ", "- **Shared trials:** ")).AppendLine(c.SharedTrials.ToString());
            foreach (var milestone in c.Milestones) sb.Append("  - ").AppendLine(milestone);
        }
        if (list.Count == 0) sb.AppendLine(Lang.T("\n_Пока никого._", "\n_Nobody yet._"));
        return sb.ToString();
    }

    internal static string BuildWorldstateMd(RpgState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Lang.T("# Состояние мира", "# State of the world"));
        sb.AppendLine();
        sb.AppendLine(Lang.T("> Зеркало книги героя: сводка «здесь и сейчас» (set_world_state) и глобальные события (update_world_event).", "> A mirror of the Hero's Book: the \"here and now\" summary (set_world_state) and global events (update_world_event)."));
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(state.WorldState))
        {
            sb.AppendLine(state.WorldState.Trim());
        }
        else
        {
            sb.AppendLine(Lang.T("_Мир пока спокоен; герой в начале пути._", "_The world is calm for now; the hero is at the start of the journey._"));
        }

        sb.AppendLine().Append(Lang.T("- **Время:** ", "- **Time:** ")).AppendLine(FileTools.TimeText(state));

        // Тайные часы здесь не пишем: файл может открыть игрок, а их видит только мастер в сводке состояния.
        var clocks = state.Clocks.Where(c => !c.Secret).ToList();
        if (clocks.Count > 0)
        {
            sb.AppendLine().AppendLine(Lang.T("## Часы", "## Clocks")).AppendLine();
            foreach (var c in clocks)
            {
                sb.Append("- **").Append(c.Name).Append(":** ").Append(c.Done ? Lang.T("свершилось", "came to pass") : $"{c.Filled}/{c.Segments}")
                  .AppendLine(string.IsNullOrWhiteSpace(c.OnFill) ? "" : $" — {c.OnFill}");
            }
        }

        if (state.Factions.Count > 0)
        {
            sb.AppendLine().AppendLine(Lang.T("## Фракции", "## Factions")).AppendLine();
            foreach (var f in state.Factions)
            {
                sb.Append("- **").Append(f.Name).Append(":** ").Append(FactionStanding.Label(f.Standing))
                  .Append($" ({f.Standing:+#;-#;0})").AppendLine(string.IsNullOrWhiteSpace(f.Note) ? "" : $" — {f.Note}");
            }
        }

        var events = state.WorldEvents.Where(e => !string.IsNullOrWhiteSpace(e.Name)).ToList();
        if (events.Count > 0)
        {
            sb.AppendLine().AppendLine(Lang.T("## Глобальные события", "## Global events")).AppendLine();
            foreach (var e in events)
            {
                sb.Append("### ").AppendLine(e.Name!.Trim());
                sb.Append(Lang.T("- **Статус:** ", "- **Status:** ")).AppendLine(e.State);
                if (!string.IsNullOrWhiteSpace(e.Description))
                {
                    sb.Append(Lang.T("- **Что происходит:** ", "- **What is happening:** ")).AppendLine(e.Description.Trim());
                }

                if (!string.IsNullOrWhiteSpace(e.Impact))
                {
                    sb.Append(Lang.T("- **Может коснуться героя:** ", "- **May affect the hero:** ")).AppendLine(e.Impact.Trim());
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    internal static string BuildInstancesMd(RpgState state)
    {
        var sb = new StringBuilder(Lang.T("# Изолированные игровые сцены\n\n", "# Isolated game scenes\n\n"));
        sb.AppendLine(Lang.T("> Зеркало передач между основным Мастером и суб-мастерами (start_instance / finish_instance). Ручные правки перезапишутся.", "> A mirror of handoffs between the main GM and sub-GMs (start_instance / finish_instance). Manual edits will be overwritten."));
        if (state.ActiveInstance is { Active: true } active)
        {
            sb.Append(Lang.T("\n## Сейчас: ", "\n## Now: ")).AppendLine(active.Title);
            sb.Append(Lang.T("- **Тип:** ", "- **Type:** ")).AppendLine(active.Kind);
            sb.Append(Lang.T("- **Условие выхода:** ", "- **Exit condition:** ")).AppendLine(active.ExitCondition);
        }
        foreach (var h in state.InstanceHandoffs.AsEnumerable().Reverse())
        {
            sb.Append("\n## ").Append(h.Title).Append(Lang.T(" — завершено\n\n", " — completed\n\n"));
            sb.AppendLine(h.Summary);
            void Line(string label, string value) { if (!string.IsNullOrWhiteSpace(value)) sb.Append("- **").Append(label).Append(":** ").AppendLine(value); }
            Line(Lang.T("Отношения", "Relationships"), h.Relationships); Line(Lang.T("Добыча", "Loot"), h.Loot); Line(Lang.T("Ресурсы", "Resources"), h.Resources);
            Line(Lang.T("Последствия", "Consequences"), h.Consequences); Line(Lang.T("Незакрытые нити", "Open threads"), h.OpenThreads);
        }
        if (state.ActiveInstance is not { Active: true } && state.InstanceHandoffs.Count == 0) sb.AppendLine(Lang.T("\n_Изолированных сцен ещё не было._", "\n_There have been no isolated scenes yet._"));
        return sb.ToString();
    }

    internal static string WorldTemplate => CampaignTemplates.WorldTemplate;

    internal static string JournalTemplate => CampaignTemplates.JournalTemplate;

    internal static string RulesTemplate => CampaignTemplates.RulesTemplate;

    // ===== Имена файлов =====

    private static string Cell(string? text)
    {
        var t = (text ?? "").Trim().Replace("|", "/");
        return t.Replace("\r", " ").Replace("\n", " ");
    }

    internal static string SafeFileName(string? name)
    {
        var clean = (name ?? "").Trim();
        foreach (var ch in Path.GetInvalidFileNameChars())
        {
            clean = clean.Replace(ch, '_');
        }

        // Вертикальная черта в markdown-таблицах заменяется на '/', но в имени файла
        // слэш стал бы разделителем каталогов. Здесь нужен безопасный обычный символ.
        clean = clean.Replace('|', '_').Trim().TrimEnd('.');
        if (clean.Length == 0)
        {
            clean = Lang.T("Без имени", "Unnamed");
        }

        if (clean.Length > 60)
        {
            clean = clean[..60].TrimEnd();
        }

        return clean;
    }

    private static string UniqueFileName(HashSet<string> used, string baseName)
    {
        var candidate = used.Add(baseName + ".md")
            ? baseName + ".md"
            : null;

        if (candidate is not null)
        {
            return candidate;
        }

        var i = 2;
        while (true)
        {
            candidate = $"{baseName} ({i}).md";
            if (used.Add(candidate))
            {
                return candidate;
            }

            i++;
        }
    }
}
