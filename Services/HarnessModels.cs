using System.Text.Json.Serialization;

namespace RPG_Harness.Services;

/// <summary>Настройки подключения к LLM API и рабочая директория.</summary>
public sealed class HarnessSettings
{
    /// <summary>Базовый URL OpenAI-совместимого API, например https://api.openai.com/v1.</summary>
    public string ApiBaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>API-ключ (Bearer). Может быть пустым для локальных серверов.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Идентификатор модели.</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>Системный промпт мастера, подставляется перед историей при каждом запросе.</summary>
    public string SystemPrompt { get; set; } = GmPrompt.Default;

    /// <summary>
    /// Отслеживает ли промпт базовый из кода. true — да, при выпуске новой версии он подставляется
    /// автоматически; false — правка игрока, не трогаем; null — файл старого формата (решаем при загрузке).
    /// </summary>
    public bool? SystemPromptIsDefault { get; set; }

    /// <summary>
    /// Системный промпт мастера для сеттинга «Киберпанк» (свой: мир, классы, добыча, торговцы, колесо улиц).
    /// Отправляется вместо <see cref="SystemPrompt"/>, когда выбрана тема «Киберпанк». Пусто — базовый из кода.
    /// </summary>
    public string SystemPromptCyber { get; set; } = GmPrompt.CyberDefault;

    /// <summary>Отслеживает ли киберпанковый промпт базовый из кода (как <see cref="SystemPromptIsDefault"/>).</summary>
    public bool SystemPromptCyberIsDefault { get; set; } = true;

    /// <summary>Системный промпт мастера для сеттинга «Современность» (приземлённый мир наших дней).</summary>
    public string SystemPromptModern { get; set; } = GmPrompt.ModernDefault;

    /// <summary>Отслеживает ли современный промпт базовый из кода.</summary>
    public bool SystemPromptModernIsDefault { get; set; } = true;

    /// <summary>Английский промпт мастера для сеттинга «Фэнтези» (язык интерфейса — English).</summary>
    public string SystemPromptEn { get; set; } = GmPrompt.DefaultEn;

    /// <summary>Отслеживает ли английский фэнтезийный промпт базовый из кода.</summary>
    public bool SystemPromptEnIsDefault { get; set; } = true;

    /// <summary>Английский промпт мастера для сеттинга «Киберпанк».</summary>
    public string SystemPromptCyberEn { get; set; } = GmPrompt.CyberDefaultEn;

    public bool SystemPromptCyberEnIsDefault { get; set; } = true;

    /// <summary>Английский промпт мастера для сеттинга «Современность».</summary>
    public string SystemPromptModernEn { get; set; } = GmPrompt.ModernDefaultEn;

    public bool SystemPromptModernEnIsDefault { get; set; } = true;

    /// <summary>
    /// Язык интерфейса и мастера (Lang.Ru / Lang.En). Пусто — язык ещё не выбран: при первом запуске
    /// интерфейс спросит его отдельным окном.
    /// </summary>
    public string Language { get; set; } = "";

    /// <summary>Язык приложения по настройкам (пусто → русский, пока игрок не выбрал).</summary>
    [JsonIgnore]
    public string ActiveLanguage => RPG_Harness.Services.Lang.Normalize(Language);

    /// <summary>Промпт мастера для сеттинга на текущем языке приложения.</summary>
    public string PromptFor(string genre) => PromptFor(genre, RPG_Harness.Services.Lang.Current);

    /// <summary>Промпт мастера для сеттинга и языка.</summary>
    public string PromptFor(string genre, string language)
    {
        var en = RPG_Harness.Services.Lang.Normalize(language) == RPG_Harness.Services.Lang.En;
        return RPG_Harness.Services.Genre.Normalize(genre) switch
        {
            RPG_Harness.Services.Genre.Cyberpunk => en ? SystemPromptCyberEn : SystemPromptCyber,
            RPG_Harness.Services.Genre.Modern => en ? SystemPromptModernEn : SystemPromptModern,
            _ => en ? SystemPromptEn : SystemPrompt,
        };
    }

    /// <summary>Записывает промпт мастера для сеттинга и языка (редактор в настройках).</summary>
    public void SetPrompt(string genre, string language, string value)
    {
        var en = RPG_Harness.Services.Lang.Normalize(language) == RPG_Harness.Services.Lang.En;
        switch (RPG_Harness.Services.Genre.Normalize(genre))
        {
            case RPG_Harness.Services.Genre.Cyberpunk when en: SystemPromptCyberEn = value; break;
            case RPG_Harness.Services.Genre.Cyberpunk: SystemPromptCyber = value; break;
            case RPG_Harness.Services.Genre.Modern when en: SystemPromptModernEn = value; break;
            case RPG_Harness.Services.Genre.Modern: SystemPromptModern = value; break;
            default:
                if (en) SystemPromptEn = value;
                else SystemPrompt = value;
                break;
        }
    }

    /// <summary>Сколько последних сообщений отправлять модели (0 — всю историю).
    /// Долгая память кампании живёт в файлах и книге героя.</summary>
    public int HistoryLimit { get; set; } = 60;

    /// <summary>Заявленный размер контекстного окна модели в токенах.</summary>
    public int MaxContextTokens { get; set; } = 128000;

    /// <summary>При каком заполнении контекста автоматически сокращать старую историю; 0 — выключено.</summary>
    public int AutoCompressPercent { get; set; } = 85;

    /// <summary>Вести изолированные сцены (данжи, затяжные бои и т.п.) в отдельном контексте суб-мастера.</summary>
    public bool SeparateInstanceSessions { get; set; }

    /// <summary>Модель понимает изображения (в чате появляется кнопка прикрепления картинки).</summary>
    public bool VisionEnabled { get; set; } = false;

    /// <summary>Рабочая директория: в ней хранится папка chats с сохранёнными диалогами.</summary>
    public string WorkDirectory { get; set; } = "";

    /// <summary>Отправлять параметры рассуждений (reasoning effort / thinking).</summary>
    public bool ReasoningEnabled { get; set; } = false;

    /// <summary>Формат параметров рассуждений: "openai" (reasoning_effort) или "deepseek" (thinking + reasoning_effort).</summary>
    public string ReasoningFormat { get; set; } = "openai";

    /// <summary>Уровни, которые поддерживает модель (отмечаются в настройках и предлагаются в профилях).</summary>
    public List<string> ReasoningLevels { get; set; } = new() { "low", "medium", "high" };

    /// <summary>Температура и глубина рассуждений отдельно для каждого вида запроса (ключ — RequestKinds.*).</summary>
    public Dictionary<string, RequestProfile> Profiles { get; set; } = RequestKinds.DefaultProfiles();

    /// <summary>Тема оформления (UiThemes.*): пусто или старое значение → «Фэнтези».</summary>
    public string Theme { get; set; } = UiThemes.Fantasy;

    /// <summary>Сеттинг игры по теме оформления.</summary>
    [JsonIgnore]
    public string ActiveGenre => UiThemes.Normalize(Theme);

    /// <summary>Профиль для вида запроса (с запасным вариантом по умолчанию).</summary>
    public RequestProfile ProfileFor(string kind) =>
        Profiles.TryGetValue(kind, out var p) ? p
        : RequestKinds.DefaultProfiles().TryGetValue(kind, out var d) ? d
        : new RequestProfile();

    public HarnessSettings Clone()
    {
        var copy = (HarnessSettings)MemberwiseClone();
        copy.ReasoningLevels = new List<string>(ReasoningLevels);
        copy.Profiles = Profiles.ToDictionary(kv => kv.Key, kv => kv.Value.Clone());
        return copy;
    }
}

/// <summary>Параметры генерации для одного вида запроса.</summary>
public sealed class RequestProfile
{
    /// <summary>Температура; null — не отправлять (значение модели по умолчанию).</summary>
    public double? Temperature { get; set; }

    /// <summary>Уровень рассуждений (none, minimal, low, medium, high, xhigh, max); пусто — по умолчанию модели.</summary>
    public string Effort { get; set; } = "";

    public RequestProfile Clone() => (RequestProfile)MemberwiseClone();
}

/// <summary>Виды запросов к мастеру — у каждого своя температура и глубина рассуждений.</summary>
public static class RequestKinds
{
    public const string World = "world";
    public const string Story = "story";
    public const string Combat = "combat";
    public const string Trade = "trade";
    public const string Ooc = "ooc";

    public static (string Key, string Title, string Hint)[] All => Lang.IsEn ? AllEn : AllRu;

    private static readonly (string Key, string Title, string Hint)[] AllRu =
    {
        (World, "Создание мира и арок", "Сессия ноль и выбор арки: нужна фантазия и разнообразие."),
        (Story, "Приключение", "Обычные сцены: исследование, диалоги, описания."),
        (Combat, "Бой", "Есть противники: точный учёт ХП, бросков и правил."),
        (Trade, "Торговля", "Итог сделки и реплики торговца: цены и количество без выдумок."),
        (Ooc, "Вне игры", "Режим «Игрок»: разбор правил и спорных моментов — точно и по делу."),
    };

    private static readonly (string Key, string Title, string Hint)[] AllEn =
    {
        (World, "World and arc building", "Session zero and choosing an arc: needs imagination and variety."),
        (Story, "Adventure", "Ordinary scenes: exploration, dialogue, descriptions."),
        (Combat, "Combat", "Adversaries present: exact tracking of HP, rolls and rules."),
        (Trade, "Trading", "Deal outcome and merchant lines: prices and quantities without invention."),
        (Ooc, "Out of game", "\"Player\" mode: discussing rules and disputes — precise and to the point."),
    };

    /// <summary>Все известные уровни рассуждений (OpenAI: none…xhigh; DeepSeek: high/max).</summary>
    public static (string Key, string Title)[] Levels => new[]
    {
        ("none", Lang.T("none — без рассуждений", "none — no reasoning")),
        ("minimal", "minimal"),
        ("low", "low"),
        ("medium", "medium"),
        ("high", "high"),
        ("xhigh", "xhigh"),
        ("max", "max"),
    };

    public static Dictionary<string, RequestProfile> DefaultProfiles() => new()
    {
        [World] = new RequestProfile { Temperature = 1.0, Effort = "high" },
        [Story] = new RequestProfile { Temperature = 1.0, Effort = "medium" },
        [Combat] = new RequestProfile { Temperature = 0.8, Effort = "medium" },
        [Trade] = new RequestProfile { Temperature = 0.8, Effort = "low" },
        [Ooc] = new RequestProfile { Temperature = 0.8, Effort = "medium" },
    };

    public static string TitleOf(string kind) => All.FirstOrDefault(k => k.Key == kind).Title ?? kind;
}

/// <summary>Одно сообщение диалога (role: system | user | assistant).</summary>
public sealed class ChatMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";

    /// <summary>Вызовы инструментов (для role=assistant). null — обычное сообщение.</summary>
    public List<ToolCall>? ToolCalls { get; set; }

    /// <summary>Прикреплённые изображения (data URI image/jpeg | image/png). Только для role=user.</summary>
    public List<string>? Images { get; set; }

    /// <summary>Сообщение «вне игры»: игрок говорит с мастером как человек, а не как персонаж
    /// (у ответа мастера тоже true — он отвечает вне игры).</summary>
    public bool Ooc { get; set; }

    /// <summary>Рассуждения модели (reasoning_content) — DeepSeek требует возвращать их при tool calls.</summary>
    public string? Reasoning { get; set; }

    /// <summary>Служебное сообщение харнеса: уходит модели, но не показывается в чате.</summary>
    public bool Hidden { get; set; }

    /// <summary>Идентификатор изолированной сцены. Такие сообщения видны игроку, но не засоряют основной контекст.</summary>
    public string? InstanceId { get; set; }

    public ChatMessage() { }

    public ChatMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }

    [JsonIgnore]
    public bool HasImages => Images is { Count: > 0 };
}

/// <summary>Вызов инструмента моделью: запрос + сохранённый результат выполнения.</summary>
public sealed class ToolCall
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Аргументы в виде сырого JSON (как прислала модель).</summary>
    public string Arguments { get; set; } = "";

    /// <summary>Результат выполнения: null — ещё не выполнен.</summary>
    public bool? Ok { get; set; }

    /// <summary>Текст результата (он же отправляется модели как role=tool).</summary>
    public string? Result { get; set; }
}

/// <summary>Диалог: сохраняется отдельным JSON-файлом.</summary>
public sealed class ChatSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Title { get; set; } = Lang.T("Новый чат", "New chat");
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ChatMessage> Messages { get; set; } = new();

    /// <summary>Расход токенов за всю сохранённую сессию и за последний полный ход игрока.</summary>
    public TokenUsageStats TotalUsage { get; set; } = new();
    public TokenUsageStats LastTurnUsage { get; set; } = new();

    /// <summary>Флаг «идёт генерация», в файл не сохраняется.</summary>
    [JsonIgnore]
    public bool Busy { get; set; }
}

/// <summary>Usage одного или нескольких запросов к модели; cache hit/miss считаются в токенах.</summary>
public sealed class TokenUsageStats
{
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long CacheHitTokens { get; set; }
    public long CacheMissTokens { get; set; }

    public void Add(TokenUsageStats? other)
    {
        if (other is null) return;
        InputTokens += other.InputTokens;
        OutputTokens += other.OutputTokens;
        CacheHitTokens += other.CacheHitTokens;
        CacheMissTokens += other.CacheMissTokens;
    }
}

// ===== RPG-состояние кампании =====

/// <summary>RPG-заметки кампании, привязанные к конкретному чату.</summary>
public sealed class RpgState
{
    /// <summary>Версия коэффициентов ХП: 1 = герой ×2, спутники ×1.5.</summary>
    public int HpScaleVersion { get; set; }

    /// <summary>
    /// Сеттинг кампании (Genre.*), фиксируется при создании героя. Пусто — старое сохранение, это фэнтези.
    /// Кампания открывается только в теме своего сеттинга.
    /// </summary>
    public string GenreId { get; set; } = "";

    public CharacterSheet Character { get; set; } = new();
    public List<InventoryItem> Inventory { get; set; } = new();
    public List<ImportantNpc> ImportantCharacters { get; set; } = new();

    /// <summary>Персонажи, которых нельзя забыть из-за долга, обещания, вопроса или связанного задания.</summary>
    public List<ImportantNpc> Notables { get; set; } = new();

    /// <summary>Постоянные участники группы (не более трёх).</summary>
    public List<PartyMember> Party { get; set; } = new();

    /// <summary>Знакомые, которые могут стать спутниками после совместной сцены и взаимного согласия.</summary>
    public List<CompanionCandidate> CompanionCandidates { get; set; } = new();

    /// <summary>Купленные слухи и подсказки. Их точное содержание скрыто от обычного повествования.</summary>
    public List<RumorClue> RumorClues { get; set; } = new();

    /// <summary>Текущая или последняя изолированная сцена.</summary>
    public InstanceSession? ActiveInstance { get; set; }

    /// <summary>Краткие передачи завершённых инстансов основному мастеру.</summary>
    public List<InstanceHandoff> InstanceHandoffs { get; set; } = new();

    /// <summary>Активные противники (их статистику ведёт мастер/модель).</summary>
    public List<Adversary> Adversaries { get; set; } = new();
    public List<GridItem> BattleLoot { get; set; } = new();

    /// <summary>
    /// Очередь хода текущего боя: брошенная инициатива в порядке бросания. Пусто — инициатива не брошена.
    /// Хранится здесь, а не в боевом журнале: журнал живёт в памяти и пропадает при перезапуске,
    /// а очередь нужна модели в каждой сводке состояния — иначе она забывает порядок и пропускает ходы.
    /// </summary>
    public List<InitiativeSeat> Initiative { get; set; } = new();

    /// <summary>
    /// Текущий раунд и действующий участник. В старых сохранениях 0/пусто означает, что очередь ещё
    /// не запущена. Это не даёт модели повторять или пропускать ходы после обрезки контекста.
    /// </summary>
    public int CombatRound { get; set; }
    public string CombatCurrentActor { get; set; } = "";

    /// <summary>Города и точки интереса, придуманные для мира.</summary>
    public List<CityRecord> Cities { get; set; } = new();

    /// <summary>Задания, принятые игроком.</summary>
    public List<QuestRecord> Quests { get; set; } = new();

    /// <summary>Инвентарь-тетрис: предметы с позициями на сетке 6x8.</summary>
    public List<GridItem> Grid { get; set; } = new();

    /// <summary>Сюжетные арки кампании.</summary>
    public List<StoryArc> StoryArcs { get; set; } = new();

    /// <summary>Глобальные события мира (войны, стихии, праздники) — ведёт мастер.</summary>
    public List<WorldEventRecord> WorldEvents { get; set; } = new();

    /// <summary>Сводка текущего состояния мира: где герой, время, погода, что меняется.</summary>
    public string WorldState { get; set; } = "";

    /// <summary>
    /// Текущее место сцены (поселение, лес, данж), где играется действие. Меняет только харнес —
    /// по travel и границам изолированной сцены; по нему же фильтруется «Взаимодействия неподалёку».
    /// </summary>
    public string ScenePlace { get; set; } = "";

    /// <summary>
    /// Открытое разовое предложение уличного продавца (не больше одного). Продавец — не NPC:
    /// в списках персонажей он не живёт, это событие одного разговора.
    /// </summary>
    public StreetOffer? PendingStreetOffer { get; set; }

    /// <summary>
    /// Персонажи, доступные прямо сейчас: категория nearby и место, где их встретили, совпадают
    /// с текущей сценой. Старые сохранения (пустое место) видны, пока место сцены тоже пустое.
    /// </summary>
    public IEnumerable<ImportantNpc> NearbyHere() => ImportantCharacters.Where(n =>
        n.Category == "nearby" &&
        !string.IsNullOrWhiteSpace(n.Name) &&
        string.Equals(n.NearbyPlace ?? "", ScenePlace ?? "", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// «Разрешения» на легендарную добычу: выдаются колесом дороги (легендарная находка) и завершением арки.
    /// Без разрешения мастер не может выдать герою легендарную вещь — она станет эпической.
    /// </summary>
    public int LegendaryTokens { get; set; }

    /// <summary>
    /// Копилка магической добычи (LootTables): каждый бой добавляет долю по силе врагов, находка добротной
    /// и выше вещи списывает единицу. Защищает от «засухи» и от потопа добычи: в среднем ~3 находки на уровень.
    /// Старт 0.5 — первая находка приходит в первых боях.
    /// </summary>
    public double LootMeter { get; set; } = 0.5;

    /// <summary>id недавних событий колеса дороги — чтобы события не повторялись подряд.</summary>
    public List<string> RecentEncounters { get; set; } = new();

    /// <summary>
    /// День кампании (с 1) и часть суток (0 утро, 1 день, 2 вечер, 3 ночь). Время двигают харнес-инструменты:
    /// долгий отдых, дорога, set_world_state. От дней тикают часы угрозы — мир живёт без героя.
    /// </summary>
    public int Day { get; set; } = 1;
    public int DayPart { get; set; }

    /// <summary>Часы угрозы и прогресса (как в Blades in the Dark): заполняются тиками, по заполнению — событие.</summary>
    public List<StoryClock> Clocks { get; set; } = new();

    /// <summary>Отношение фракций к герою: число −100…100 и метка, которую считает харнес.</summary>
    public List<FactionStanding> Factions { get; set; } = new();

    /// <summary>Коротких отдыхов с последнего долгого (их не больше двух).</summary>
    public int ShortRests { get; set; }

    /// <summary>Отдых, прерванный ночной тревогой (short / long): его можно продолжить через rest resume=true.</summary>
    public string PendingRest { get; set; } = "";

    /// <summary>День последнего долгого отдыха (0 — не было): два долгих отдыха подряд утром одного дня не даём.</summary>
    public int LastLongRestDay { get; set; }

    /// <summary>Идёт ли бой: есть противник, который ещё сражается (не повержен, не бежал и не сдался).</summary>
    public bool InCombat() => Adversaries.Any(a => a.InFight);

    /// <summary>Папка кампании внутри {WorkDirectory}/campaigns (у каждого чата — своя).</summary>
    public string CampaignFolder { get; set; } = "";

    /// <summary>Размер инвентаря-тетриса в клетках.</summary>
    public const int GridCols = 11;
    public const int GridRows = 5;
}

/// <summary>
/// Разовая уличная сделка: прохожий предлагает купить одну вещь без лавки. Вещь и цену подобрал
/// харнес, сам продающий в списки персонажей не попадает — это событие одного разговора.
/// </summary>
public sealed class StreetOffer
{
    /// <summary>Как прохожий назвал себя — только для текста сделки, не для списков NPC.</summary>
    public string Seller { get; set; } = "";

    /// <summary>Что продаёт: координаты клетки займутся при кладении в сумку.</summary>
    public GridItem Item { get; set; } = new();

    /// <summary>Цена за всё предложение, зафиксированная в момент предложения.</summary>
    public int Price { get; set; }

    /// <summary>Вариант подбора: rare — редкая вещь, cheap — дешёвая мелочь.</summary>
    public string Kind { get; set; } = "cheap";
}

/// <summary>Брошенная инициатива одного участника боя: имя и итог броска (кубик + мод ЛОВ).</summary>
public sealed class InitiativeSeat
{
    /// <summary>Кто бросал: «Герой», «Лира», «Гоблин-разбойник 1». Имя — как в книге боя.</summary>
    public string Name { get; set; } = "";

    /// <summary>Итог броска — с него считается место в очереди.</summary>
    public int Total { get; set; }
}

/// <summary>Боец группы (герой или спутник): уровень, характеристики и надетое — из них считаются КБ и атака.</summary>
public interface ICombatant
{
    string Name { get; }
    string CharClass { get; }

    /// <summary>Боевой архетип (ключ <see cref="Progression.Archetypes"/>); пусто — угадывается по классу.</summary>
    string Role { get; }

    string Level { get; }
    Dictionary<string, EquippedItem> Equipment { get; }
    int Stat(DndStat stat);
}

/// <summary>Лист персонажа игрока.</summary>
public sealed class CharacterSheet : ICombatant
{
    public string Name { get; set; } = "";
    public string Race { get; set; } = "";
    public string CharClass { get; set; } = "";
    public string Gender { get; set; } = "";

    /// <summary>Боевой архетип (tank, striker, caster…); пусто — угадывается по классу.</summary>
    public string Role { get; set; } = "";

    /// <summary>Портрет героя: id из <see cref="HeroAvatars.All"/> (например «mage-female»).
    /// Пусто — портрет подбирается по классу и полу.</summary>
    public string Avatar { get; set; } = "";

    public string Level { get; set; } = "";

    /// <summary>Надетая экипировка по слотам (ключ — имя EquipSlot).</summary>
    public Dictionary<string, EquippedItem> Equipment { get; set; } = new();

    /// <summary>Текущее / максимальное здоровье.</summary>
    public int HpCurrent { get; set; }
    public int HpMax { get; set; }

    /// <summary>Мана (0/0 — класс без магии).</summary>
    public int ManaCurrent { get; set; }
    public int ManaMax { get; set; }
    /// <summary>Временный энергетический щит; поглощает урон раньше HP.</summary>
    public int Shield { get; set; }

    public List<SkillRecord> Skills { get; set; } = new();

    /// <summary>Золото.</summary>
    public int Gold { get; set; }

    /// <summary>Опыт.</summary>
    public int Xp { get; set; }

    /// <summary>Репутация в общей сети гильдий приключенцев. Ранг вычисляется по ней.</summary>
    public int GuildReputation { get; set; }

    /// <summary>Шесть классических характеристик (10 = среднее).</summary>
    public int Str { get; set; } = 10;
    public int Dex { get; set; } = 10;
    public int Con { get; set; } = 10;
    public int Int { get; set; } = 10;
    public int Wis { get; set; } = 10;
    public int Cha { get; set; } = 10;

    /// <summary>Активные состояния/эффекты (отравление, благословение…).</summary>
    public List<string> Status { get; set; } = new();

    /// <summary>Состояние при 0 HP: dying | stable | dead. Пусто у старых живых героев.</summary>
    public string DeathState { get; set; } = "";
    public int DeathSaveSuccesses { get; set; }
    public int DeathSaveFailures { get; set; }

    public string Notes { get; set; } = "";

    public int Stat(DndStat stat) => stat switch
    {
        DndStat.Str => Str,
        DndStat.Dex => Dex,
        DndStat.Con => Con,
        DndStat.Int => Int,
        DndStat.Wis => Wis,
        _ => Cha,
    };

    public void SetStat(DndStat stat, int value)
    {
        value = Math.Clamp(value, 1, 30);
        switch (stat)
        {
            case DndStat.Str: Str = value; break;
            case DndStat.Dex: Dex = value; break;
            case DndStat.Con: Con = value; break;
            case DndStat.Int: Int = value; break;
            case DndStat.Wis: Wis = value; break;
            default: Cha = value; break;
        }
    }
}

/// <summary>Классические характеристики D&D.</summary>
public enum DndStat { Str, Dex, Con, Int, Wis, Cha }

public static class DndStatNames
{
    /// <summary>Полные названия по индексу enum.</summary>
    public static string[] Full => Lang.IsEn
        ? new[] { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" }
        : new[] { "Сила", "Ловкость", "Телосложение", "Интеллект", "Мудрость", "Харизма" };

    /// <summary>Короткие обозначения по индексу enum.</summary>
    public static string[] Short => Lang.IsEn
        ? new[] { "STR", "DEX", "CON", "INT", "WIS", "CHA" }
        : new[] { "СИЛ", "ЛОВ", "ТЕЛ", "ИНТ", "МДР", "ХАР" };

    /// <summary>Модификатор характеристики по правилам D&D: (значение − 10) / 2 вниз.</summary>
    public static int Modifier(int value) => (int)Math.Floor((value - 10) / 2.0);

    public static string ModText(int value)
    {
        var m = Modifier(value);
        return m >= 0 ? $"+{m}" : m.ToString();
    }
}

/// <summary>Слоты экипировки персонажа.</summary>
public enum EquipSlot
{
    Helmet,   // шлем
    Body,     // торс
    Gloves,   // перчатки
    Boots,    // ботинки
    Amulet,   // амулет
    Ring1,    // кольцо 1
    Ring2,    // кольцо 2
    Belt,     // пояс
    Hand1,    // рука 1 (или двуручное)
    Hand2,    // рука 2 (пуста при двуручном)
    Cloak,    // плащ / накидка
}

/// <summary>Надетый предмет в слоте экипировки.</summary>
public sealed class EquippedItem
{
    public string Name { get; set; } = "";

    /// <summary>Идентификатор пиксельной иконки (класс .pix-<id>).</summary>
    public string Icon { get; set; } = "";

    /// <summary>Зачарование / свойства / заметка.</summary>
    public string Note { get; set; } = "";

    /// <summary>Двуручное оружие занимает обе руки.</summary>
    public bool TwoHanded { get; set; }

    /// <summary>Размер в клетках — чтобы снятый предмет вернулся в сумку тем же прямоугольником.</summary>
    public int W { get; set; } = 1;
    public int H { get; set; } = 1;

    /// <summary>Цена и редкость переезжают вместе с предметом между сумкой и слотом.</summary>
    public int Value { get; set; }
    public string Rarity { get; set; } = "common";

    /// <summary>Урон оружия, например «1d8 рубящий» (пусто — не оружие).</summary>
    public string Damage { get; set; } = "";

    /// <summary>Бонус к классу брони (доспех, щит, шлем…).</summary>
    public int Armor { get; set; }

    /// <summary>Числовые бонусы: str/dex/con/int/wis/cha/attack/damage/ac/hp/mana → значение.</summary>
    public Dictionary<string, int> Bonuses { get; set; } = new();

    /// <summary>Абстрактные свойства и умения предмета («невидимость ночью», «навык: взлом»).</summary>
    public List<string> Effects { get; set; } = new();

    /// <summary>Уровень предмета: от него растут бюджет бонусов и цена (0 — не задан, считается 1).</summary>
    public int Level { get; set; }
}

/// <summary>Предмет, лежащий в клетке инвентаря-тетриса.</summary>
public sealed class GridItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n")[..8];
    public string Name { get; set; } = "";

    /// <summary>Идентификатор пиксельной иконки (класс .pix-<id>).</summary>
    public string Icon { get; set; } = "sack";

    /// <summary>Позиция и размер в клетках инвентаря (RpgState.GridCols x RpgState.GridRows).</summary>
    public int Col { get; set; }
    public int Row { get; set; }
    public int W { get; set; } = 1;
    public int H { get; set; } = 1;

    /// <summary>Количество (для стакающихся мелочей вроде зелий).</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Предмет расходуется при применении. Для зелий, еды, свитков и книг также определяется по каталогу.</summary>
    public bool Consumable { get; set; }

    /// <summary>Заметка: зачарование, квестовое назначение и т.п.</summary>
    public string Note { get; set; } = "";

    /// <summary>В какой слот экипировки можно надеть (null — нельзя).</summary>
    public EquipSlot? Slot { get; set; }

    /// <summary>Двуручное оружие занимает обе руки.</summary>
    public bool TwoHanded { get; set; }

    /// <summary>Цена за штуку в золоте (0 — по каталогу с учётом редкости).</summary>
    public int Value { get; set; }

    /// <summary>Редкость: common | uncommon | rare | epic | legendary.</summary>
    public string Rarity { get; set; } = "common";

    /// <summary>Урон оружия, например «1d8 рубящий» (пусто — не оружие).</summary>
    public string Damage { get; set; } = "";

    /// <summary>Бонус к классу брони (доспех, щит, шлем…).</summary>
    public int Armor { get; set; }

    /// <summary>Числовые бонусы: str/dex/con/int/wis/cha/attack/damage/ac/hp/mana → значение.</summary>
    public Dictionary<string, int> Bonuses { get; set; } = new();

    /// <summary>Абстрактные свойства и умения предмета («невидимость ночью», «навык: взлом»).</summary>
    public List<string> Effects { get; set; } = new();

    /// <summary>Уровень предмета: от него растут бюджет бонусов и цена (0 — не задан, считается 1).</summary>
    public int Level { get; set; }
}

/// <summary>Предмет в старом списочном инвентаре (до инвентаря-тетриса; оставлен для чтения старых сохранений).</summary>
public sealed class InventoryItem
{
    public string Name { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public string Note { get; set; } = "";
}

/// <summary>Важный NPC кампании (союзник или нейтральный).</summary>
public sealed class ImportantNpc
{
    public string Name { get; set; } = "";

    /// <summary>Кто это, чем важен, где обитает.</summary>
    public string Note { get; set; } = "";

    /// <summary>Отношение к герою: дружелюбное / нейтральное / враждебное…</summary>
    public string Attitude { get; set; } = "";

    /// <summary>Уровень или «мощь» NPC (может быть пустым).</summary>
    public string Level { get; set; } = "";

    /// <summary>Ключевые способности, умения, снаряжение.</summary>
    public string Abilities { get; set; } = "";

    public int HpCurrent { get; set; }
    public int HpMax { get; set; }

    /// <summary>Занятие или роль в мире: кузнец, капитан стражи, жрица…</summary>
    public string Role { get; set; } = "";

    /// <summary>Где обычно находится (город / точка интереса).</summary>
    public string Location { get; set; } = "";

    /// <summary>important — близкий/доверенный; nearby — доступен в текущей сцене.</summary>
    public string Category { get; set; } = "important";

    /// <summary>
    /// Место сцены, в котором NPC сейчас «рядом»; пусто — до первой смены места (старые сохранения).
    /// Привязку ставит харнес: при появлении nearby и когда герой уходит из места.
    /// </summary>
    public string NearbyPlace { get; set; } = "";

    /// <summary>Лавка, если NPC торгует (null — не торгует).</summary>
    public MerchantShop? Shop { get; set; }
}

/// <summary>
/// Лавка торговца: что он продаёт и покупает, по каким ценам и сколько у него денег.
/// Товары лежат на своей сетке (как сумка героя).
/// </summary>
public sealed class MerchantShop
{
    /// <summary>Тип торговца (ключ из ItemEconomy.Merchants): blacksmith, alchemist, general…</summary>
    public string Kind { get; set; } = "general";

    /// <summary>Вывеска: «Кузница „Молот и наковальня“».</summary>
    public string Title { get; set; } = "";

    /// <summary>Размер поселения: village | town | city — влияет на ассортимент и деньги.</summary>
    public string Tier { get; set; } = "town";

    /// <summary>Сколько золота у торговца (ограничивает скупку).</summary>
    public int Gold { get; set; } = 200;

    /// <summary>Наценка при продаже герою (1.0 — базовая цена).</summary>
    public double Markup { get; set; } = 1.0;

    /// <summary>Доля цены, которую торговец платит за «свои» товары.</summary>
    public double BuyRate { get; set; } = 0.5;

    /// <summary>Доля цены за товары из смежных категорий (0 — вообще не берёт).</summary>
    public double OffRate { get; set; } = 0.15;

    /// <summary>Основные категории (торгует ими и охотно скупает). Пусто — по типу торговца.</summary>
    public List<string> Buys { get; set; } = new();

    /// <summary>Смежные категории (скупает дёшево, по OffRate). Пусто — по типу торговца.</summary>
    public List<string> Also { get; set; } = new();

    /// <summary>Товары на сетке лавки (ShopCols x ShopRows).</summary>
    public List<GridItem> Items { get; set; } = new();

    public const int ShopCols = 11;
    public const int ShopRows = 10;
}

/// <summary>Противник в активной сцене.</summary>
public sealed class Adversary
{
    public string Name { get; set; } = "";
    public string Level { get; set; } = "";
    public int HpCurrent { get; set; }
    public int HpMax { get; set; }
    public int Shield { get; set; }
    public string Portrait { get; set; } = "";
    /// <summary>ordinary | elite | quest_boss | dungeon_boss | arc_boss.</summary>
    public string ThreatTier { get; set; } = "ordinary";
    public List<string> Effects { get; set; } = new();

    /// <summary>Состояния (опьянён, в ловушке…).</summary>
    public string Status { get; set; } = "";

    /// <summary>Атаки, способности, тактика.</summary>
    public string Abilities { get; set; } = "";

    public string Notes { get; set; } = "";

    /// <summary>Архетип статблока: standard | brute | skirmisher | defender | caster | sniper.</summary>
    public string Archetype { get; set; } = "";

    /// <summary>Класс брони (0 — не задан).</summary>
    public int Ac { get; set; }

    /// <summary>Бонус атаки и число атак за ход.</summary>
    public int AttackBonus { get; set; }
    public int Attacks { get; set; }

    /// <summary>Урон одной атаки: «2d8+3 рубящий».</summary>
    public string Damage { get; set; } = "";

    /// <summary>Сложность спасбросков против его эффектов.</summary>
    public int SaveDc { get; set; }

    /// <summary>Шкала стойкости босса: текущее / максимум (0 — нет шкалы).</summary>
    public int Stagger { get; set; }
    public int StaggerMax { get; set; }

    /// <summary>Легендарных действий за раунд.</summary>
    public int Legendary { get; set; }

    /// <summary>Тип существа (humanoid, beast, undead…) — от него зависят добыча и слабости; пусто — по портрету.</summary>
    public string Kind { get; set; } = "";

    /// <summary>Опыт за этого противника уже начислен (award_xp) — повторно не считается.</summary>
    public bool XpGiven { get; set; }

    /// <summary>
    /// Мораль: пусто — сражается; fled — бежал; surrendered — сдался. Бежавший и сдавшийся выходят из боя
    /// с ХП: за них дают опыт, сдавшийся оставляет добычу, бежавший — нет.
    /// </summary>
    public string Morale { get; set; } = "";

    /// <summary>Какие проверки морали уже бросались (ранен / пал вожак / полег отряд) — каждая один раз.</summary>
    public List<string> MoraleTests { get; set; } = new();

    /// <summary>Ещё в бою: жив и не бежал/не сдался.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool InFight => HpCurrent > 0 && string.IsNullOrEmpty(Morale);

    /// <summary>Добыча с него уже разыграна (roll_loot).</summary>
    public bool LootRolled { get; set; }
}

/// <summary>Город/поселение мира.</summary>
public sealed class CityRecord
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Только в крупных городах есть полноценное отделение гильдии приключенцев.</summary>
    public bool IsMajor { get; set; }
    public AdventureGuild? AdventureGuild { get; set; }
    public List<PoiRecord> Points { get; set; } = new();

    /// <summary>Знакомые с положительным отношением, важные только для этого поселения.</summary>
    public List<LocalAcquaintance> Acquaintances { get; set; } = new();
}

public sealed class AdventureGuild
{
    public string Name { get; set; } = Lang.T("Гильдия приключенцев", "Adventurers' Guild");
    public string Steward { get; set; } = "";
    public string Description { get; set; } = "";
    public List<GuildJob> Jobs { get; set; } = new();
}

public sealed class GuildJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n")[..8];
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string MinRank { get; set; } = "F";
    public string Difficulty { get; set; } = Lang.T("обычное", "ordinary");
    public int GoldReward { get; set; }
    public int ReputationReward { get; set; }
    public string ItemReward { get; set; } = "";
    public string State { get; set; } = "available"; // available | accepted | completed | expired
}

public static class GuildRanks
{
    public static (string Rank, int Min, string Title)[] All => Lang.IsEn ? AllEn : AllRu;

    private static readonly (string Rank, int Min, string Title)[] AllRu =
    {
        ("F", 0, "Новичок"), ("E", 100, "Искатель"), ("D", 300, "Следопыт"),
        ("C", 700, "Ветеран"), ("B", 1500, "Мастер"), ("A", 3000, "Герой"), ("S", 6000, "Легенда")
    };

    private static readonly (string Rank, int Min, string Title)[] AllEn =
    {
        ("F", 0, "Novice"), ("E", 100, "Seeker"), ("D", 300, "Pathfinder"),
        ("C", 700, "Veteran"), ("B", 1500, "Master"), ("A", 3000, "Hero"), ("S", 6000, "Legend")
    };

    public static string For(int reputation) => All.Last(x => reputation >= x.Min).Rank;
    public static int Index(string? rank) => Array.FindIndex(All, x => x.Rank.Equals(rank, StringComparison.OrdinalIgnoreCase)) is var i && i >= 0 ? i : 0;
    public static bool CanTake(int reputation, string? minRank) => Index(For(reputation)) >= Index(minRank);
    public static string TitleFor(int reputation) { var x = All.Last(v => reputation >= v.Min); return $"{x.Rank} · {x.Title}"; }
}

public sealed class LocalAcquaintance
{
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string Note { get; set; } = "";
}

/// <summary>Точка интереса в городе (таверна, храм, рынок…).</summary>
public sealed class PoiRecord
{
    public string Name { get; set; } = "";

    /// <summary>Тип: таверна / храм / рынок / гильдия / кузница / стража…</summary>
    public string Type { get; set; } = "";

    public string Note { get; set; } = "";
}

/// <summary>Задание, принятое игроком.</summary>
public sealed class QuestRecord
{
    public string Name { get; set; } = "";

    /// <summary>Что нужно сделать и зачем.</summary>
    public string Description { get; set; } = "";

    /// <summary>accepted | done | failed.</summary>
    public string State { get; set; } = "accepted";

    /// <summary>Откуда задание: имя NPC или организации.</summary>
    public string Giver { get; set; } = "";

    /// <summary>Награда (золото, предмет, репутация).</summary>
    public string Reward { get; set; } = "";

    /// <summary>Отметки о ходе выполнения.</summary>
    public List<string> Progress { get; set; } = new();
}

/// <summary>Сюжетная арка — крупная линия кампании.</summary>
public sealed class StoryArc
{
    public string Name { get; set; } = "";

    /// <summary>Известная игроку завязка без будущих событий и спойлеров.</summary>
    public string Premise { get; set; } = "";

    /// <summary>Устаревший внутренний план арки. Игроку не показывается.</summary>
    public List<string> Beats { get; set; } = new();

    /// <summary>Хронология уже произошедших в рамках арки событий.</summary>
    public List<string> Events { get; set; } = new();

    /// <summary>active | done | abandoned.</summary>
    public string State { get; set; } = "active";

    /// <summary>Главный антагонист или противодействующая сила.</summary>
    public string Antagonist { get; set; } = "";

    /// <summary>Короткая внутренняя подсказка мастеру о следующем шаге большой арки.</summary>
    public string NextLead { get; set; } = "";

    /// <summary>Разрешение на легендарную награду за финал уже выдано; защищает от повторной награды после реактивации.</summary>
    public bool CompletionRewardGiven { get; set; }
}

/// <summary>Купленная подсказка: хранится точно, но раскрывается только через действие «попробовать вспомнить».</summary>
public sealed class RumorClue
{
    public string Source { get; set; } = "";
    public string LinkedTo { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Tier { get; set; } = "minor";
    public bool Recalled { get; set; }
}

/// <summary>
/// Кандидат в спутники. Это ещё самостоятельный NPC, а не скрытый четвёртый боец:
/// запись хранит путь знакомства и не даёт мастеру мгновенно материализовать готового компаньона.
/// </summary>
public sealed class CompanionCandidate
{
    public string Name { get; set; } = "";
    public string Race { get; set; } = "";
    public string Gender { get; set; } = "";
    public string Portrait { get; set; } = "";

    /// <summary>arc | guild | world | hireling | history.</summary>
    public string Source { get; set; } = "world";
    public string Arc { get; set; } = "";

    /// <summary>Кем персонаж полезен и интересен в истории, без боевых чисел.</summary>
    public string Concept { get; set; } = "";
    public string Motivation { get; set; } = "";
    public string PersonalGoal { get; set; } = "";
    public string JoinCondition { get; set; } = "";

    /// <summary>Видимая игроку зацепка, почему с этим человеком стоит продолжить общение.</summary>
    public string PlayerHint { get; set; } = "";

    /// <summary>introduced | proven | ready | refused | unavailable | recruited.</summary>
    public string Stage { get; set; } = "introduced";
    public int SharedTrials { get; set; }
    public List<string> Milestones { get; set; } = new();
    public bool RoleOffered { get; set; }
    public string SelectedRole { get; set; } = "";
}

/// <summary>Самостоятельный участник группы. Инвентарь доступен для просмотра, но меняется только с согласием.</summary>
public sealed class PartyMember : ICombatant
{
    public string Name { get; set; } = "";
    public string Race { get; set; } = "";
    public string CharClass { get; set; } = "";
    public string Gender { get; set; } = "";
    public string Level { get; set; } = "";

    /// <summary>Роль в группе — боевой архетип (tank, striker, healer…); пусто — угадывается по классу.</summary>
    public string Role { get; set; } = "";

    /// <summary>Надетое снаряжение (те же слоты, что у героя): из него считаются КБ и атака спутника.</summary>
    public Dictionary<string, EquippedItem> Equipment { get; set; } = new();
    public int HpCurrent { get; set; }
    public int HpMax { get; set; }
    public int ManaCurrent { get; set; }
    public int ManaMax { get; set; }
    public int Shield { get; set; }
    public string Portrait { get; set; } = "";
    /// <summary>Активные состояния и эффекты (отравление, благословение, оглушение…).</summary>
    public List<string> Status { get; set; } = new();
    /// <summary>Состояние при 0 HP и счёт спасбросков от смерти.</summary>
    public string DeathState { get; set; } = "";
    public int DeathSaveSuccesses { get; set; }
    public int DeathSaveFailures { get; set; }
    public int Str { get; set; } = 10;
    public int Dex { get; set; } = 10;
    public int Con { get; set; } = 10;
    public int Int { get; set; } = 10;
    public int Wis { get; set; } = 10;
    public int Cha { get; set; } = 10;
    public List<string> Skills { get; set; } = new();
    public List<SkillRecord> LearnedSkills { get; set; } = new();
    public List<InventoryItem> Inventory { get; set; } = new();
    public List<GridItem> Grid { get; set; } = new();
    public string Note { get; set; } = "";

    public int Stat(DndStat stat) => stat switch
    {
        DndStat.Str => Str,
        DndStat.Dex => Dex,
        DndStat.Con => Con,
        DndStat.Int => Int,
        DndStat.Wis => Wis,
        _ => Cha,
    };

    public void SetStat(DndStat stat, int value)
    {
        value = Math.Clamp(value, 1, 30);
        switch (stat)
        {
            case DndStat.Str: Str = value; break;
            case DndStat.Dex: Dex = value; break;
            case DndStat.Con: Con = value; break;
            case DndStat.Int: Int = value; break;
            case DndStat.Wis: Wis = value; break;
            default: Cha = value; break;
        }
    }
}

public sealed class SkillRecord
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "attack"; // attack support heal cleanse shield revive debuff utility
    public int Rank { get; set; } = 1;
    public int ManaCost { get; set; }
    public string Target { get; set; } = "enemy";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "skill";
    public string Evolution { get; set; } = "";
}

/// <summary>Отдельный контекст суб-мастера для данжа, боя или другой замкнутой сцены.</summary>
public sealed class InstanceSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n")[..10];
    public string Kind { get; set; } = "other";
    public string Title { get; set; } = Lang.T("Изолированная сцена", "Isolated scene");
    public string Brief { get; set; } = "";
    public string ExitCondition { get; set; } = "";
    public bool Active { get; set; } = true;

    /// <summary>Место сцены до входа — возвращается в состояние при finish_instance.</summary>
    public string PrevScenePlace { get; set; } = "";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public List<ChatMessage> Messages { get; set; } = new();
}

public sealed class InstanceHandoff
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Relationships { get; set; } = "";
    public string Loot { get; set; } = "";
    public string Resources { get; set; } = "";
    public string Consequences { get; set; } = "";
    public string OpenThreads { get; set; } = "";
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Глобальное событие мира, влияющее на сюжет.</summary>
public sealed class WorldEventRecord
{
    public string Name { get; set; } = "";

    /// <summary>Что происходит и на что влияет.</summary>
    public string Description { get; set; } = "";

    /// <summary>ongoing | upcoming | ended.</summary>
    public string State { get; set; } = "ongoing";

    /// <summary>Как событие может коснуться героя.</summary>
    public string Impact { get; set; } = "";
}

/// <summary>
/// Часы угрозы или прогресса: N сегментов, которые заполняются тиками (событиями сцены или ходом дней).
/// Заполнились — происходит заранее объявленное событие (on_fill). Тайные часы видит только мастер.
/// </summary>
public sealed class StoryClock
{
    public string Name { get; set; } = "";

    /// <summary>threat — угроза (злодей, бедствие), progress — дело героя, rival — соперник.</summary>
    public string Kind { get; set; } = "threat";

    public int Segments { get; set; } = 6;
    public int Filled { get; set; }

    /// <summary>Тайные часы видит только мастер в сводке состояния, игрок — нет.</summary>
    public bool Secret { get; set; }

    /// <summary>Тикает сам раз в N дней (0 — только по событиям сцены).</summary>
    public int PerDays { get; set; }

    /// <summary>Последний день, с которого считается авто-тик.</summary>
    public int LastTickDay { get; set; }

    /// <summary>Что произойдёт, когда часы заполнятся.</summary>
    public string OnFill { get; set; } = "";

    /// <summary>Часы заполнились и событие объявлено мастеру — больше не тикают.</summary>
    public bool Done { get; set; }
}

/// <summary>Отношение фракции к герою: −100 (кровные враги) … +100 (союзники).</summary>
public sealed class FactionStanding
{
    public string Name { get; set; } = "";
    public int Standing { get; set; }
    public string Note { get; set; } = "";

    /// <summary>Последние причины изменений (до пяти) — чтобы мастер помнил, за что любят или ненавидят.</summary>
    public List<string> History { get; set; } = new();

    public static string Label(int standing) => standing switch
    {
        <= -60 => Lang.T("враги", "enemies"),
        <= -20 => Lang.T("враждебны", "hostile"),
        < 20 => Lang.T("нейтральны", "neutral"),
        < 60 => Lang.T("дружелюбны", "friendly"),
        _ => Lang.T("союзники", "allies"),
    };
}
