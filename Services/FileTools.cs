using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RPG_Harness.Services;

/// <summary>
/// Инструменты модели (tool calls).
/// Файлы: create_file, edit_file, read_file, list_files — пути жёстко ограничены папкой кампании.
/// Книга героя: update_character, set_stat, update_npc, update_adversary, update_city, update_quest, update_arc,
/// update_world_event, set_world_state, equip_item, update_grid_item.
/// Торговля: set_merchant, update_shop_item, open_trade.
/// Игра: roll_dice (честные броски), offer_choices и propose_arcs (кнопки выбора в чате).
/// </summary>
public sealed partial class FileTools
{
    public const int MaxListEntries = 300;
    public const int DefaultReadChars = 20000;
    public const int MaxReadChars = 100000;

    /// <summary>Инструменты, после которых ход мастера заканчивается: игрок должен выбрать.</summary>
    public static readonly string[] WaitForPlayerTools = { "offer_choices", "propose_arcs", "open_trade", "open_adventure_guild", "ask_companion_role" };

    private readonly RpgStateStore _rpg;
    private readonly ItemCatalog _catalog;
    private readonly ItemEconomy _economy;
    private readonly SettingsService _settings;

    public FileTools(RpgStateStore rpg, ItemCatalog catalog, ItemEconomy economy, SettingsService settings)
    {
        _rpg = rpg;
        _catalog = catalog;
        _economy = economy;
        _settings = settings;
    }

    /// <summary>Строка на текущем языке (короткая запись для описаний и результатов инструментов).</summary>
    private static string L(string ru, string en) => Lang.T(ru, en);

    /// <summary>Схемы и их длина отдельно для каждого сеттинга и языка: каталоги иконок, портретов и торговцев у них разные.</summary>
    private readonly Dictionary<string, (JsonArray Defs, int Chars)> _definitions = new();
    private readonly object _definitionsGate = new();

    /// <summary>Схемы инструментов текущего сеттинга для поля tools (OpenAI function calling).</summary>
    public JsonArray Definitions => DefinitionsFor(Genre.Current + "/" + Lang.Current).Defs;

    /// <summary>
    /// Длина схем инструментов в символах реального текста — для оценки контекста. ToJsonString() экранирует кириллицу
    /// escape-последовательностями (6 символов на букву) и завышал оценку втрое, из-за чего история сокращалась раньше времени.
    /// </summary>
    public int DefinitionsChars => DefinitionsFor(Genre.Current + "/" + Lang.Current).Chars;

    /// <summary>Описания инструментов и параметров — в термины сеттинга (эдди, ОЗУ, фиксер…).</summary>
    private static void LocalizeDescriptions(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(kv => kv.Key).ToList())
                {
                    if (key == "description" && obj[key] is JsonValue v && v.TryGetValue<string>(out var text))
                    {
                        obj[key] = Genre.Localize(text);
                    }
                    else
                    {
                        LocalizeDescriptions(obj[key]);
                    }
                }

                break;
            case JsonArray arr:
                foreach (var item in arr)
                {
                    LocalizeDescriptions(item);
                }

                break;
        }
    }

    private (JsonArray Defs, int Chars) DefinitionsFor(string key)
    {
        lock (_definitionsGate)
        {
            if (_definitions.TryGetValue(key, out var cached))
            {
                return cached;
            }

            // Каталоги и справочники читают Genre.Current и Lang.Current; строим схемы, когда этот сеттинг и язык активны.
            var defs = BuildDefinitions();
            foreach (var def in ExtraDefinitions().Concat(TacticsDefinitions()))
            {
                defs.Add(def);
            }

            if (!Genre.IsFantasy)
            {
                LocalizeDescriptions(defs);
            }

            var chars = System.Text.Json.JsonSerializer.Serialize(defs,
                new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }).Length;
            return _definitions[key] = (defs, chars);
        }
    }

    private JsonArray BuildDefinitions() => new()
    {
        Def("create_file",
            L("Создать текстовый файл в папке кампании (или перезаписать существующий при overwrite=true). " +
            "Незаполненные шаблоны (World.md, Rules.md, Factions.md, Bestiary.md, Secrets.md) можно заполнять без overwrite — сохраняй их заголовки.",
            "Create a text file in the campaign folder (or overwrite an existing one with overwrite=true). " +
            "Unfilled templates (World.md, Rules.md, Factions.md, Bestiary.md, Secrets.md) can be filled without overwrite — keep their headings."),
            ("path", "string", L("Относительный путь файла, например World.md или Characters/Лира.md", "Relative file path, for example World.md or Characters/Lyra.md"), true),
            ("content", "string", L("Полное содержимое файла (UTF-8, markdown)", "Full file content (UTF-8, markdown)"), true),
            ("overwrite", "boolean", L("Перезаписать, если файл уже существует. По умолчанию false.", "Overwrite if the file already exists. Default false."), false)),
        Def("edit_file",
            L("Заменить фрагмент текста в существующем файле кампании. Чтобы дописать в конец (например, в Journal.md), передай append.",
              "Replace a text fragment in an existing campaign file. To append to the end (for example, to Journal.md), pass append."),
            ("path", "string", L("Относительный путь файла", "Relative file path"), true),
            ("find", "string", L("Искомый фрагмент (точное совпадение). Не нужен, если передан append.", "The fragment to find (exact match). Not needed if append is passed."), false),
            ("replace", "string", L("Текст, на который нужно заменить", "The replacement text"), false),
            ("append", "string", L("Дописать этот текст в конец файла (с новой строки)", "Append this text to the end of the file (on a new line)"), false),
            ("all", "boolean", L("Заменить все вхождения. По умолчанию только первое.", "Replace all occurrences. By default only the first."), false)),
        Def("read_file",
            L("Прочитать текстовый файл кампании.", "Read a campaign text file."),
            ("path", "string", L("Относительный путь файла", "Relative file path"), true),
            ("max_chars", "integer", L("Максимум символов результата (по умолчанию 20000)", "Maximum characters of the result (default 20000)"), false)),
        Def("list_files",
            L("Показать файлы и папки кампании.", "List the campaign files and folders."),
            ("path", "string", L("Относительный путь подпапки (по умолчанию корень папки кампании)", "Relative path of a subfolder (default — the campaign folder root)"), false),
            ("recursive", "boolean", L("Обходить вложенные папки", "Walk nested folders"), false)),
        Def("roll_dice",
            L("Честный бросок костей: ловушки, падения, таблицы, броски NPC вне боя — никогда не выдумывай результат сам. Результат увидит и игрок. " +
            "Атаки, навыки и лечение в бою разыгрывай resolve_attack, проверки героя — skill_check, инициативу — initiative roll_all, события в пути — travel. " +
            "Если всё же бросаешь здесь атаку или спасбросок, заполняй attacker, target, check и dc — журнал боя расшифрует бросок; урон — отдельным вызовом с тем же attacker, итог — hp_delta с source.",
            "A fair dice roll: traps, falls, tables, NPC rolls outside combat — never make up the result yourself. The player sees the result too. " +
            "Resolve attacks, skills and healing in combat with resolve_attack, hero checks with skill_check, initiative with initiative roll_all, travel events with travel. " +
            "If you still roll an attack or a saving throw here, fill in attacker, target, check and dc — the battle log will decode the roll; damage — with a separate call with the same attacker, the result — hp_delta with source."),
            ("expression", "string", L("Формула: 1d20+3, 2d6, 4d6kh3 (оставить 3 лучших), d100, 1d20 adv / 1d20 dis (преимущество/помеха)", "Formula: 1d20+3, 2d6, 4d6kh3 (keep the 3 best), d100, 1d20 adv / 1d20 dis (advantage/disadvantage)"), true),
            ("reason", "string", L("Зачем бросок: «атака гоблина», «урон топором», «случайная встреча»", "Why the roll: \"goblin attack\", \"axe damage\", \"random encounter\""), false),
            ("attacker", "string", L("Кто бросает: «Герой», «Сильвана», «Гоблин-разбойник 1»", "Who rolls: \"Hero\", \"Sylvana\", \"Goblin Raider 1\""), false),
            ("target", "string", L("По кому или против кого бросок: имя цели либо «Герой»", "At whom or against whom the roll is made: the target's name or \"Hero\""), false),
            ("check", "string", L("Тип броска: attack (атака), save (спасбросок), damage (урон), check (проверка), initiative (инициатива)", "Roll type: attack, save (saving throw), damage, check, initiative"), false),
            ("dc", "integer", L("Число, которое нужно побить: класс брони цели для атаки или СЛ спасброска", "The number to beat: the target's armor class for an attack or the saving throw DC"), false),
            ("bonus", "integer", L("Суммарный модификатор броска (атрибут + владение + предметы), например 4", "The total roll modifier (ability + proficiency + items), for example 4"), false)),
        Def("initiative",
            L("Очередь хода боя. В начале боя вызови roll_all=true — харнес сам бросит инициативу всем (герой и спутники — d20 + ЛОВ, противники — по архетипу) " +
            "и начнёт раунд 1; засаду отметь ambush. Очередь хранится в кампании и видна в сводке строкой «Очередь хода» и игроку бейджами в окне боя. " +
            "Без параметров — прочитать очередь; name и value — вписать участника (подкрепление посреди боя) или исправить число; " +
            "name и remove — убрать; clear — сбросить перед новым боем или полным перебросом.",
            "The combat turn order. At the start of combat call roll_all=true — the harness rolls initiative for everyone itself (the hero and companions — d20 + DEX, adversaries — by archetype) " +
            "and starts round 1; mark an ambush with ambush. The queue is stored in the campaign and is visible in the summary as the \"Turn order\" line and to the player as badges in the battle window. " +
            "Without parameters — read the queue; name and value — add a participant (reinforcements mid-fight) or correct a number; " +
            "name and remove — remove; clear — reset before a new fight or a full reroll."),
            ("roll_all", "boolean", L("Бросить инициативу всем участникам без места в очереди и начать раунд 1", "Roll initiative for all participants without a place in the queue and start round 1"), false),
            ("ambush", "string", L("Для roll_all: heroes — засаду устроили герои, enemies — враги (их сторона бросает с преимуществом)", "For roll_all: heroes — the heroes set the ambush, enemies — the enemies did (their side rolls with advantage)"), false),
            ("name", "string", L("Кто: имя участника боя (Герой, Лира, Гоблин-разбойник 1)", "Who: the name of a combat participant (Hero, Lyra, Goblin Raider 1)"), false),
            ("value", "integer", L("Инициатива участника — поставит или заменит его число в очереди", "The participant's initiative — sets or replaces their number in the queue"), false),
            ("remove", "boolean", L("Убрать участника из очереди", "Remove the participant from the queue"), false),
            ("clear", "boolean", L("Сбросить всю очередь (новый бой или переброс инициативы всем)", "Reset the whole queue (a new fight or rerolling initiative for everyone)"), false)),
        Def("combat_turn",
            L("Вести проверяемый порядок боя. После инициативы вызови action=start. После полного действия КАЖДОГО участника вызывай action=advance с его именем: " +
            "харнес не даст повторить или пропустить ход и сам увеличит номер раунда. Без параметров/action=read — узнать текущий ход; end — остановить счётчик после победы, бегства или капитуляции.",
            "Run a verifiable combat order. After initiative call action=start. After the full action of EACH participant call action=advance with their name: " +
            "the harness won't let a turn be repeated or skipped and increments the round number itself. Without parameters/action=read — find out the current turn; end — stop the counter after victory, flight or surrender."),
            ("action", "string", "start / advance / read / end", false),
            ("actor", "string", L("Кто только что завершил ход; обязателен для advance", "Who just finished their turn; required for advance"), false)),
        Def("resolve_death_save",
            L("Честно бросить и записать спасбросок от смерти персонажа при 0 HP. Харнес хранит успехи/провалы, учитывает натуральные 1/20, стабилизацию и смерть. " +
            "Не используй обычный roll_dice для спасбросков от смерти.",
            "Fairly roll and record a death saving throw for a character at 0 HP. The harness keeps successes/failures, accounts for natural 1/20, stabilization and death. " +
            "Do not use an ordinary roll_dice for death saves."),
            ("target", "string", L("hero / Герой / имя героя либо точное имя спутника", "hero / Hero / the hero's name or a companion's exact name"), true)),
        Def("travel",
            Genre.IsModern
                ? L("Колесо дороги: вызывай КАЖДЫЙ раз, когда герой отправляется дальше текущей сцены (через город, в пригород, по трассе в другой город, за границу) — до того, как описать путь. " +
                  "Программа честно бросает d100: 60% — спокойный путь, 40% — случайное событие из каталога. Значимость события зависит от дальности: " +
                  "medium — через город или в пригород (полчаса–пара часов на машине): в основном мелкие встречи; long — в другой город по трассе или поездом (часы–день): " +
                  "бывают засады, погони и крупные находки; epic — в другой регион или страну (самолёт, граница, два броска): шанс на крупное и легендарное. " +
                  "Перемещения внутри квартала или здания (short) не бросаются. Результат — суть ситуации, варианты (можно ли уехать или избежать) и редкость добычи: " +
                  "опиши событие своими словами в тоне кампании, дай герою выбор (offer_choices), выдавай добычу не выше выпавшей редкости. ",
                  "The road wheel: call it EVERY time the hero heads beyond the current scene (across the city, to the suburbs, along the highway to another city, abroad) — before describing the way. " +
                  "The program fairly rolls d100: 60% — a calm trip, 40% — a random event from the catalog. The weight of the event depends on the distance: " +
                  "medium — across the city or to the suburbs (half an hour–a couple of hours by car): mostly minor encounters; long — to another city by highway or train (hours–a day): " +
                  "ambushes, chases and big finds happen; epic — to another region or country (a plane, a border, two rolls): a chance of something big and legendary. " +
                  "Moves inside a block or a building (short) are not rolled. The result is the gist of the situation, the options (whether one can drive off or avoid it) and the loot rarity: " +
                  "describe the event in your own words in the campaign's tone, give the hero a choice (offer_choices), hand out loot no higher than the rolled rarity. ")
                : Genre.IsCyber
                ? L("Колесо улиц: вызывай КАЖДЫЙ раз, когда герой отправляется дальше текущей сцены (в другой район, промзону, Пустоши, другой город) — до того, как описать путь. " +
                  "Программа честно бросает d100: 60% — спокойный путь, 40% — случайное событие из каталога. Значимость события зависит от дальности: " +
                  "medium — соседний район или промзона (час–вечер): в основном мелкие находки и встречи; long — через весь мегаполис или в соседний город через Пустоши (дни): " +
                  "бывают логова банд и заброшенные объекты; epic — в другой мегаполис или страну (недели, два броска): шанс на крупное и культовое. " +
                  "Перемещения внутри здания или квартала (short) не бросаются. Результат — суть ситуации, варианты (можно ли уйти или сбежать) и редкость добычи: " +
                  "опиши событие своими словами в тоне кампании, дай герою выбор (offer_choices), выдавай добычу не выше выпавшей редкости. ",
                  "The street wheel: call it EVERY time the hero heads beyond the current scene (another district, an industrial zone, the Wastelands, another city) — before describing the way. " +
                  "The program fairly rolls d100: 60% — a calm trip, 40% — a random event from the catalog. The weight of the event depends on the distance: " +
                  "medium — a neighboring district or an industrial zone (an hour–an evening): mostly minor finds and encounters; long — across the whole megacity or to a neighboring city through the Wastelands (days): " +
                  "gang hideouts and abandoned facilities happen; epic — to another megacity or country (weeks, two rolls): a chance of something big and iconic. " +
                  "Moves inside a building or a block (short) are not rolled. The result is the gist of the situation, the options (whether one can leave or flee) and the loot rarity: " +
                  "describe the event in your own words in the campaign's tone, give the hero a choice (offer_choices), hand out loot no higher than the rolled rarity. ")
                : L("Колесо дороги: вызывай КАЖДЫЙ раз, когда герой отправляется в путь дальше пределов поселения — до того, как описать дорогу. " +
                  "Программа честно бросает d100: 60% — спокойный путь, 40% — случайное событие из каталога. Значимость события зависит от дальности: " +
                  "medium — выход за город (к логову, на квест в окрестностях; часы–день пути): в основном мелкие находки и встречи; " +
                  "long — путь между городами (дни): бывают логова, подземелья; epic — путь между странами/регионами (недели, два броска): шанс на крупное и легендарное. " +
                  "Перемещения внутри города (short) не бросаются. Результат — суть ситуации, варианты (можно ли отказаться или сбежать) и редкость добычи: " +
                  "опиши событие своими словами в мире и тоне кампании, дай герою выбор (offer_choices), выдавай добычу не выше выпавшей редкости. ",
                  "The road wheel: call it EVERY time the hero sets out beyond the settlement — before describing the road. " +
                  "The program fairly rolls d100: 60% — a calm trip, 40% — a random event from the catalog. The weight of the event depends on the distance: " +
                  "medium — a trip outside the town (to a lair, a quest in the surroundings; hours–a day of travel): mostly minor finds and encounters; " +
                  "long — a journey between towns (days): lairs and dungeons happen; epic — a journey between countries/regions (weeks, two rolls): a chance of something big and legendary. " +
                  "Moves inside a town (short) are not rolled. The result is the gist of the situation, the options (whether one can refuse or flee) and the loot rarity: " +
                  "describe the event in your own words in the world and tone of the campaign, give the hero a choice (offer_choices), hand out loot no higher than the rolled rarity. ") +
            TravelEncounters.PromptReference(),
            ("from", "string", L("Откуда идёт герой", "Where the hero is coming from"), true),
            ("to", "string", L("Куда идёт герой", "Where the hero is going"), true),
            ("distance", "string", L("Дальность: short / medium / long / epic", "Distance: short / medium / long / epic"), true),
            ("terrain", "string", Lang.IsEn
                ? Genre.Pick("The main terrain of the way: road, forest, plains, hills, mountains, swamp, desert, coast, snow (optional)",
                    "The main terrain of the way: street (streets, slums), corporate (corporate center), industrial (industrial zone, docks), wasteland (the Wastelands), underground (subway, sewers), net (a dive into the net) (optional)", "The main terrain of the way: city (the city), highway (the highway), rural (villages and dirt roads), wild (forest, mountains, taiga), industrial (industrial zone, port), border (the border, checkpoints) (optional)")
                : Genre.Pick("Основная местность пути: road, forest, plains, hills, mountains, swamp, desert, coast, snow (опционально)",
                "Основная местность пути: street (улицы, трущобы), corporate (корпоративный центр), industrial (промзона, доки), wasteland (Пустоши), underground (метро, коллекторы), net (вылазка в сеть) (опционально)", "Основная местность пути: city (город), highway (трасса), rural (сёла и просёлки), wild (лес, горы, тайга), industrial (промзона, порт), border (граница, блокпосты) (опционально)"), false)),
        Def("street_offer",
            L("Разовая уличная сделка: прохожий предлагает герою купить одну вещь — без лавки и окна торговли. " +
            "Харнес сам подбирает вещь (только редкую или дешёвую) и честную цену — не сочиняй ни то, ни другое. " +
            "Продавец остаётся разовым событием: для него не нужен update_npc и он не попадает в списки персонажей.",
            "A one-off street deal: a passer-by offers the hero one item to buy — without a shop or a trade window. " +
            "The harness picks the item (only a rare or a cheap one) and a fair price itself — do not make up either. " +
            "The seller remains a one-off event: no update_npc is needed for them and they do not get into the character lists."),
            ("seller", "string", L("Как прохожий назвал себя — имя для текста сделки (опционально)", "What the passer-by called themselves — a name for the deal text (optional)"), false)),
        Def("street_deal",
            L("Закрыть уличную сделку: купить предложенное или отказаться. Одновременно может быть открыто только одно предложение.",
              "Close a street deal: buy what is offered or refuse. Only one offer can be open at a time."),
            ("action", "string", L("buy — купить: списывает золото и кладёт вещь в сумку (по умолчанию) / reject — отказаться, ничего не списывая", "buy — buy: deducts the gold and puts the item into the bag (default) / reject — refuse without deducting anything"), false)),
        Def("start_instance",
            L("Передать замкнутую игровую сцену отдельному суб-мастеру с собственным контекстом. Используй только если настройка раздельных сессий включена и сцена достаточно продолжительная: данж, многораундовый бой, погоня, проникновение, испытание. Игрок остаётся в том же чате. Передай всю информацию, без которой сцена исказится.",
              "Hand a self-contained game scene to a separate sub-master with its own context. Use it only if the separate sessions setting is on and the scene is long enough: a dungeon, a multi-round battle, a chase, an infiltration, a trial. The player stays in the same chat. Pass all the information without which the scene would be distorted."),
            ("kind", "string", "dungeon / combat / chase / infiltration / trial / other", true),
            ("title", "string", L("Короткое название сцены", "A short scene title"), true),
            ("brief", "string", L("Полный бриф: место, текущая ситуация, цель героя, участники, отношения, известные факты, угрозы, важные правила и последнее действие игрока", "A full brief: the place, the current situation, the hero's goal, participants, relationships, known facts, threats, important rules and the player's last action"), true),
            ("exit_condition", "string", L("Когда вернуть управление основному мастеру", "When to return control to the main master"), true)),
        Def("finish_instance",
            L("Завершить изолированную сцену и передать основному мастеру структурированную сводку. Все реальные изменения HP, предметов, заданий и NPC сначала внеси обычными инструментами; этот вызов их не дублирует.",
              "Finish an isolated scene and hand the main master a structured summary. First record all real changes of HP, items, quests and NPCs with the usual tools; this call does not duplicate them."),
            ("summary", "string", L("Кратко, что произошло и чем закончилась сцена", "Briefly, what happened and how the scene ended"), true),
            ("relationships", "string", L("Как изменились отношения и кто что теперь думает/обещал", "How relationships changed and who now thinks/promised what"), false),
            ("loot", "string", L("Полученная, потерянная или оставленная добыча", "Loot gained, lost or left behind"), false),
            ("resources", "string", L("Изменения HP, маны, золота, расходников и состояний", "Changes of HP, mana, gold, consumables and conditions"), false),
            ("consequences", "string", L("Последствия для мира, врагов и локации", "Consequences for the world, the enemies and the location"), false),
            ("open_threads", "string", L("Незакрытые вопросы и зацепки", "Open questions and leads"), false)),
        OfferChoicesDef(),
        ProposeArcsDef(),
        Def("update_character",
            L("Обновить лист персонажа игрока (панель «Книга героя»). Передавай только изменяемые поля. Используй для урона/лечения HP, расхода маны, золота, опыта и состояний.",
              "Update the player character's sheet (the \"Hero book\" panel). Pass only the fields that change. Use it for HP damage/healing, spending mana, gold, XP and conditions."),
            ("name", "string", L("Новое имя героя (опционально, только по просьбе игрока)", "The hero's new name (optional, only at the player's request)"), false),
            ("level", "integer", L("Новый уровень — только когда герой заслужил его по правилам (опционально)", "A new level — only when the hero has earned it by the rules (optional)"), false),
            ("hp_current", "integer", L("Текущее HP (опционально)", "Current HP (optional)"), false),
            ("hp_delta", "integer", L("Изменение HP: отрицательное = урон, положительное = лечение; щит поглощает урон первым", "HP change: negative = damage, positive = healing; the shield absorbs damage first"), false),
            ("hp_max", "integer", L("Максимальное HP (опционально)", "Maximum HP (optional)"), false),
            ("mana_current", "integer", L("Текущая мана (опционально)", "Current mana (optional)"), false),
            ("mana_max", "integer", L("Максимальная мана (опционально)", "Maximum mana (optional)"), false),
            ("shield", "integer", L("Текущий временный энергетический щит (опционально)", "The current temporary energy shield (optional)"), false),
            ("gold", "integer", L("Золото — новое итоговое значение (опционально)", "Gold — the new total value (optional)"), false),
            ("xp", "integer", L("Опыт — новое итоговое значение (опционально)", "XP — the new total value (optional)"), false),
            ("add_status", "string", L("Добавить состояние (отравление, благословение…) (опционально). Повторное — обновит длительность", "Add a condition (poison, blessing…) (optional). Repeating it refreshes the duration"), false),
            ("rounds", "integer", L("Длительность add_status в раундах (в бою по умолчанию — типичная для эффекта); вне боя — «до отдыха» пиши в названии", "Duration of add_status in rounds (in combat the default is typical for the effect); outside combat write \"until rest\" in the name"), false),
            ("remove_status", "string", L("Снять состояние (опционально)", "Remove a condition (optional)"), false),
            ("source", "string", L("Кто вызвал изменение — для журнала боя: «Гоблин 1» при уроне, «Лира» при лечении, «поджог» при уроне во времени (опционально)", "Who caused the change — for the battle log: \"Goblin 1\" for damage, \"Lyra\" for healing, \"burning\" for damage over time (optional)"), false),
            ("notes", "string", L("Дополнить заметки о персонаже (опционально)", "Add to the notes about the character (optional)"), false)),
        Def("set_stat",
            L("Установить значение характеристики персонажа.", "Set the value of a character's ability score."),
            ("stat", "string", L("Одна из: str, dex, con, int, wis, cha", "One of: str, dex, con, int, wis, cha"), true),
            ("value", "integer", L("Новое значение 1–30", "The new value 1–30"), true)),
        Def("update_npc",
            L("Создать или обновить персонажа мира. important — близкий/доверенный; nearby — доступен рядом для разговора/торговли; notable — не забывать из-за долга, обещания, вопроса или задания. " +
            "Nearby привязывается к текущему месту сцены: повторный category=nearby переводит NPC туда, где герой сейчас (встретил здесь, приехал вслед), и освобождает прежнее место.",
            "Create or update a world character. important — close/trusted; nearby — available nearby to talk/trade; notable — not to be forgotten because of a debt, a promise, a question or a quest. " +
            "Nearby is bound to the current scene place: calling category=nearby again moves the NPC to where the hero is now (met here, followed the hero) and releases the previous place."),
            ("name", "string", L("Имя / титул персонажа", "The character's name / title"), true),
            ("note", "string", L("Кто это, чем важен, где обитает, чего хочет (опционально)", "Who this is, why they matter, where they live, what they want (optional)"), false),
            ("attitude", "string", L("Отношение к герою: дружелюбное / нейтральное / настороженное / враждебное (опционально)", "Attitude to the hero: friendly / neutral / wary / hostile (optional)"), false),
            ("level", "string", L("Уровень или мощь (опционально)", "Level or power (optional)"), false),
            ("abilities", "string", L("Ключевые способности, умения, снаряжение (опционально)", "Key abilities, skills, equipment (optional)"), false),
            ("role", "string", L("Занятие: кузнец, трактирщица, капитан стражи (опционально)", "Occupation: blacksmith, innkeeper, captain of the guard (optional)"), false),
            ("location", "string", L("Где обычно находится: город / заведение (опционально)", "Where they usually are: a town / an establishment (optional)"), false),
            ("category", "string", L("important / nearby / notable (по умолчанию important); nearby — персонаж теперь в текущем месте сцены", "important / nearby / notable (default important); nearby — the character is now at the current scene place"), false),
            ("remove", "boolean", L("Убрать из глобальных списков персонажей (например, когда nearby больше не рядом)", "Remove from the global character lists (for example, when a nearby NPC is no longer around)"), false),
            ("hp_current", "integer", L("Текущее HP (опционально)", "Current HP (optional)"), false),
            ("hp_max", "integer", L("Максимальное HP (опционально)", "Maximum HP (optional)"), false)),
        Def("set_merchant",
            L("Сделать NPC торговцем (или обновить его лавку). Для новой лавки ассортимент генерируется автоматически по типу торговца " +
            "и размеру поселения: много полезной мелочи, экипировки меньше, добротные вещи редко, редкие — очень редко, легендарных нет. " +
            "Торговец торгует только своими категориями; смежные скупает дёшево, остальное не берёт. " +
            "Если NPC ещё нет — он создаётся. Торговец считается рядом с героем: он привязывается к текущему месту сцены, " +
            "поэтому NPC, переехавший вслед за героем, не останется в прежнем городе. ",
            "Make an NPC a merchant (or update their shop). For a new shop the assortment is generated automatically by the merchant type " +
            "and the settlement size: lots of useful small goods, less equipment, uncommon items rarely, rare ones very rarely, no legendaries. " +
            "A merchant trades only in their own categories; they buy related goods cheaply and refuse the rest. " +
            "If the NPC does not exist yet, they are created. A merchant is considered to be near the hero: they are bound to the current scene place, " +
            "so an NPC who moved after the hero will not stay in the previous town. ") + ItemEconomy.PromptReference(),
            ("npc", "string", L("Имя NPC-торговца", "The merchant NPC's name"), true),
            ("kind", "string", L("Тип торговца (см. список): blacksmith, alchemist, general…", "Merchant type (see the list): blacksmith, alchemist, general…"), false),
            ("title", "string", L("Вывеска лавки: «Кузница „Молот и наковальня“» (опционально)", "The shop sign: \"The Hammer and Anvil Forge\" (optional)"), false),
            ("tier", "string", L("Размер поселения: village / town / city — ассортимент и деньги торговца (по умолчанию town)", "Settlement size: village / town / city — the assortment and the merchant's money (default town)"), false),
            ("restock", "boolean", L("Сгенерировать ассортимент заново (завоз товара, прошло время)", "Regenerate the assortment (a new delivery, time has passed)"), false),
            ("gold", "integer", L("Деньги торговца (опционально; иначе по размеру поселения)", "The merchant's money (optional; otherwise by settlement size)"), false),
            ("markup", "number", L("Наценка при продаже герою, 0.8–2.0 (скряга, дефицит, дружба) (опционально)", "Markup when selling to the hero, 0.8–2.0 (a miser, a shortage, friendship) (optional)"), false),
            ("buy_rate", "number", L("Доля цены, которую он платит за свои товары, 0.1–0.9 (опционально)", "The share of the price they pay for their own kind of goods, 0.1–0.9 (optional)"), false),
            ("off_rate", "number", L("Доля цены за смежные товары, 0–0.5; 0 — не берёт вовсе (опционально)", "The share of the price for related goods, 0–0.5; 0 — does not buy them at all (optional)"), false),
            ("buys", "string", L("Свои категории через запятую — если нужно переопределить тип (опционально)", "Own categories, comma-separated — to override the type (optional)"), false),
            ("also", "string", L("Смежные категории через запятую (опционально)", "Related categories, comma-separated (optional)"), false)),
        Def("update_shop_item",
            L("Добавить в лавку особый товар (уникальную или сюжетную вещь), изменить цену/количество или убрать товар. " +
            "Легендарные вещи — только у крайне редких сюжетных персонажей и по огромной цене.",
            "Add a special item to a shop (a unique or story item), change the price/quantity or remove an item. " +
            "Legendary items — only from extremely rare story characters and for a huge price."),
            ("npc", "string", L("Имя торговца", "The merchant's name"), true),
            ("name", "string", L("Название товара", "The item's name"), true),
            ("icon", "string", L("id иконки из каталога (см. update_grid_item); иначе подбирается по названию", "An icon id from the catalog (see update_grid_item); otherwise picked by name"), false),
            ("quantity", "integer", L("Количество; 0 — убрать товар", "Quantity; 0 — remove the item"), false),
            ("price", "integer", L("Цена за штуку в золоте (опционально; иначе база каталога × редкость)", "Price per unit in gold (optional; otherwise the catalog base × rarity)"), false),
            ("rarity", "string", L("common / uncommon / rare / epic / legendary (опционально)", "common / uncommon / rare / epic / legendary (optional)"), false),
            ("level", "integer", L("Уровень предмета (опционально)", "Item level (optional)"), false),
            ("note", "string", L("История и описание (опционально)", "History and description (optional)"), false),
            ("damage", "string", DamageParamHelp, false),
            ("armor", "integer", ArmorParamHelp, false),
            ("bonuses", "string", BonusesParamHelp, false),
            ("effects", "string", EffectsParamHelp, false),
            ("remove", "boolean", L("Убрать товар из лавки", "Remove the item from the shop"), false)),
        Def("open_trade",
            L("Открыть игроку окно торговли с NPC-торговцем (лавка должна быть создана через set_merchant). " +
            "Вызывай, когда герой пришёл за покупками и начал торг. После вызова ход заканчивается: игрок торгует сам, " +
            "а итог сделки придёт следующим сообщением — отыграй реакцию торговца.",
            "Open the trade window with a merchant NPC for the player (the shop must be created via set_merchant). " +
            "Call it when the hero came shopping and started to trade. After the call the turn ends: the player trades themselves, " +
            "and the deal outcome arrives as the next message — play out the merchant's reaction."),
            ("npc", "string", L("Имя торговца", "The merchant's name"), true),
            ("greeting", "string", L("Реплика торговца при открытии лавки (опционально)", "The merchant's line when the shop opens (optional)"), false)),
        Def("update_adversary",
            L("Создать или обновить активного противника (статистика боя). Ищется по имени; если нет — создаётся. Для НАЧАЛА боя используй plan_encounter — он сам " +
            "рассчитает сбалансированные статблоки. Здесь — ход боя (hp_delta, эффекты, стойкость) и одиночные враги: если у нового противника не передан hp_max, " +
            "харнес подставит статблок по level и threat_tier. Удаляй противника только после award_xp и roll_loot.",
            "Create or update an active adversary (combat stats). Looked up by name; if absent, created. To START a fight use plan_encounter — it calculates " +
            "balanced stat blocks itself. This tool is for the course of combat (hp_delta, effects, poise) and single enemies: if hp_max is not passed for a new adversary, " +
            "the harness fills in a stat block by level and threat_tier. Remove an adversary only after award_xp and roll_loot."),
            ("name", "string", L("Имя противника (для нескольких одинаковых — «Гоблин 1», «Гоблин 2»)", "The adversary's name (for several identical ones — \"Goblin 1\", \"Goblin 2\")"), true),
            ("level", "string", L("Уровень или CR (опционально)", "Level or CR (optional)"), false),
            ("hp_current", "integer", L("Текущее HP (опционально)", "Current HP (optional)"), false),
            ("hp_delta", "integer", L("Изменение HP; отрицательный урон сначала снимает shield", "HP change; negative damage removes shield first"), false),
            ("hp_max", "integer", L("Максимальное HP (опционально)", "Maximum HP (optional)"), false),
            ("shield", "integer", L("Временный щит", "Temporary shield"), false),
            ("portrait", "string", L("id портрета — облик должен совпадать с существом (волк → wolf). Не указан или не похож на имя — харнес подберёт по имени. Звёздочки — облик элиты (*), босса (**), грандиозный (***). Каталог: ", "A portrait id — the look must match the creature (wolf → wolf). If not given or not matching the name, the harness picks one by name. Stars — the look of an elite (*), a boss (**), a grand one (***). Catalog: ") + PortraitCatalog.EnemyPromptList(), false),
            ("threat_tier", "string", L("Роль: minion / ordinary / elite / quest_boss / dungeon_boss / arc_boss. ordinary — рядовой враг, arc_boss — уникальный главный противник арки", "Role: minion / ordinary / elite / quest_boss / dungeon_boss / arc_boss. ordinary — a rank-and-file enemy, arc_boss — the unique main adversary of an arc"), false),
            ("archetype", "string", L("Архетип статблока: standard / brute / skirmisher / defender / caster / sniper (для авто-статблока)", "Stat block archetype: standard / brute / skirmisher / defender / caster / sniper (for an auto stat block)"), false),
            ("kind", "string", L("Тип существа: humanoid, beast, undead, construct, dragon, demon, aberration, elemental, plant, ooze, giant, celestial, monstrosity", "Creature type: humanoid, beast, undead, construct, dragon, demon, aberration, elemental, plant, ooze, giant, celestial, monstrosity"), false),
            ("stagger", "integer", L("Текущая шкала стойкости (сегменты), для элиты и боссов", "The current poise bar (segments), for elites and bosses"), false),
            ("add_effect", "string", L("Добавить баф или дебаф; повторное наложение обновляет длительность", "Add a buff or debuff; reapplying refreshes the duration"), false),
            ("rounds", "integer", L("Длительность add_effect в раундах (по умолчанию — типичная для эффекта)", "Duration of add_effect in rounds (default — typical for the effect)"), false),
            ("remove_effect", "string", L("Снять баф или дебаф", "Remove a buff or debuff"), false),
            ("morale", "string", L("fled — бежал, surrendered — сдался (вне боя, опыт за него дают), fight — снова сражается. Харнес сам проверяет мораль после урона", "fled — ran away, surrendered — gave up (out of the fight, XP is awarded for them), fight — fighting again. The harness checks morale after damage itself"), false),
            ("source", "string", L("Кто нанёс урон или наложил эффект — для журнала боя: «Герой», «Лира», «поджог» (опционально)", "Who dealt the damage or applied the effect — for the battle log: \"Hero\", \"Lyra\", \"burning\" (optional)"), false),
            ("status", "string", L("Состояние (оглушён, в ловушке…) (опционально)", "Condition (stunned, trapped…) (optional)"), false),
            ("abilities", "string", L("Атаки (бонус и урон), способности, тактика (опционально)", "Attacks (bonus and damage), abilities, tactics (optional)"), false),
            ("notes", "string", L("Прочее: КД, слабости, добыча (опционально)", "Other: AC, weaknesses, loot (optional)"), false),
            ("remove", "boolean", L("Удалить противника из списка (побеждён/сбежал)", "Remove the adversary from the list (defeated/fled)"), false)),
        Def("update_battle_loot",
            L("Добавить в окно обыска особый (сюжетный) предмет. Обычную добычу разыгрывает roll_loot — вызывай его, а этим инструментом дополняй только то, что важно истории. " +
            "Не клади добычу сразу в сумку и не добавляй золото предметом: золото меняется через update_character только после финализации.",
            "Add a special (story) item to the search window. Ordinary loot is rolled by roll_loot — call it, and use this tool only to add what matters to the story. " +
            "Do not put loot straight into the bag and do not add gold as an item: gold changes via update_character only after finalization."),
            ("name", "string", L("Понятное название предмета", "A clear item name"), true),
            ("icon", "string", L("id иконки каталога", "A catalog icon id"), false),
            ("quantity", "integer", L("Количество", "Quantity"), false),
            ("rarity", "string", "common / uncommon / rare / epic / legendary", false),
            ("note", "string", L("Описание и игровой эффект", "Description and game effect"), false),
            ("value", "integer", L("Цена продажи за штуку", "Sale price per unit"), false),
            ("level", "integer", L("Уровень предмета (по умолчанию — уровень героя)", "Item level (default — the hero's level)"), false),
            ("damage", "string", DamageParamHelp, false),
            ("armor", "integer", ArmorParamHelp, false),
            ("bonuses", "string", BonusesParamHelp, false),
            ("effects", "string", EffectsParamHelp, false)),
        Def("update_city",
            L("Создать или обновить город либо небольшое поселение с точками интереса и местными знакомыми. Ищется по имени; если нет — создаётся.",
              "Create or update a town or a small settlement with points of interest and local acquaintances. Looked up by name; if absent, created."),
            ("name", "string", L("Название города или поселения", "The name of the town or settlement"), true),
            ("description", "string", L("Краткое описание: характер, правитель, особенности (опционально)", "A short description: character, ruler, features (optional)"), false),
            ("is_major", "boolean", L("Крупный город: автоматически создаёт отделение гильдии приключенцев", "A major city: automatically creates a branch of the adventurers' guild"), false),
            ("poi_name", "string", L("Название точки интереса для добавления/обновления (опционально)", "The name of a point of interest to add/update (optional)"), false),
            ("poi_type", "string", L("Тип: таверна / храм / рынок / гильдия / кузница / стража / замок / другое (опционально)", "Type: tavern / temple / market / guild / forge / guard / castle / other (optional)"), false),
            ("poi_note", "string", L("Что там происходит, кто владелец, зачем заходить (опционально)", "What goes on there, who the owner is, why drop by (optional)"), false),
            ("remove_poi", "string", L("Удалить точку интереса по названию (опционально)", "Remove a point of interest by name (optional)"), false),
            ("acquaintance", "string", L("Положительно настроенный местный знакомый (опционально)", "A friendly local acquaintance (optional)"), false),
            ("acquaintance_role", "string", L("Роль местного знакомого (опционально)", "The local acquaintance's role (optional)"), false),
            ("acquaintance_note", "string", L("Почему знаком и как относится (опционально)", "Why they know the hero and how they feel (optional)"), false)),
        Def("update_guild_job",
            L("Создать или обновить объявление на доске гильдии приключенцев в крупном городе. Показывай только задания, доступные по рангу героя; награда и сложность растут от F к S.",
              "Create or update a posting on the adventurers' guild board in a major city. Show only quests available for the hero's rank; the reward and difficulty grow from F to S."),
            ("city", "string", L("Крупный город с отделением гильдии", "A major city with a guild branch"), true),
            ("name", "string", L("Название задания", "The quest title"), true),
            ("description", "string", L("Цель, место и известная опасность", "Goal, place and known danger"), false),
            ("min_rank", "string", L("Минимальный ранг F / E / D / C / B / A / S", "Minimum rank F / E / D / C / B / A / S"), false),
            ("difficulty", "string", L("Краткая оценка сложности", "A short difficulty estimate"), false),
            ("gold_reward", "integer", L("Золото за выполнение", "Gold for completion"), false),
            ("reputation_reward", "integer", L("Репутация гильдии за выполнение", "Guild reputation for completion"), false),
            ("item_reward", "string", L("Известная предметная награда, если есть", "A known item reward, if any"), false),
            ("state", "string", "available / accepted / completed / expired", false)),
        Def("update_guild_reputation",
            L("Изменить репутацию гильдии после подтверждённого выполнения, провала или важной услуги. Передавай итоговое значение, не дельту.",
              "Change the guild reputation after a confirmed completion, a failure or an important service. Pass the total value, not a delta."),
            ("reputation", "integer", L("Новое итоговое значение репутации, не меньше 0", "The new total reputation value, at least 0"), true),
            ("reason", "string", L("Краткая причина изменения", "A short reason for the change"), true)),
        Def("open_adventure_guild",
            L("Открыть игроку меню отделения гильдии в крупном городе: его ранг и доступные по рангу задания с заранее известными наградами. После вызова ход заканчивается.",
              "Open the guild branch menu in a major city for the player: their rank and the quests available for it with rewards known in advance. After the call the turn ends."),
            ("city", "string", L("Название текущего крупного города", "The name of the current major city"), true)),
        Def("update_quest",
            L("Добавить или обновить задание, принятое игроком. Ищется по названию; если нет — создаётся со state=accepted.",
              "Add or update a quest accepted by the player. Looked up by title; if absent, created with state=accepted."),
            ("name", "string", L("Название задания", "The quest title"), true),
            ("description", "string", L("Что нужно сделать и зачем (опционально)", "What has to be done and why (optional)"), false),
            ("giver", "string", L("Кто выдал: NPC или организация (опционально)", "Who gave it: an NPC or an organization (optional)"), false),
            ("reward", "string", L("Награда: золото, предмет, репутация (опционально)", "Reward: gold, an item, reputation (optional)"), false),
            ("progress", "string", L("Добавить отметку о ходе выполнения (опционально)", "Add a progress note (optional)"), false),
            ("state", "string", L("Статус: accepted / done / failed (опционально)", "Status: accepted / done / failed (optional)"), false)),
        Def("update_arc",
            L("Создать или обновить сюжетную арку кампании. Активная арка одна — при активации новой прежняя активная закрывается.",
              "Create or update a campaign story arc. There is one active arc — activating a new one closes the previous active one."),
            ("name", "string", L("Название арки", "The arc title"), true),
            ("premise", "string", L("Только известная игроку завязка: исходная ситуация и уже случившиеся к началу события, без будущих этапов, тайн и развязки", "Only the player-known setup: the initial situation and events that already happened before the opening, with no future stages, secrets or resolution"), false),
            ("antagonist", "string", L("Внутренняя память мастера: антагонист или противодействующая сила; игроку не показывается (опционально)", "Internal GM memory: the antagonist or opposing force; not shown to the player (optional)"), false),
            ("next_lead", "string", L("Внутренняя память мастера: актуальный следующий ориентир; игроку не показывается", "Internal GM memory: the current next lead; not shown to the player"), false),
            ("add_event", "string", L("Добавить в журнал арки одно уже произошедшее событие; никогда не записывать сюда будущий план", "Append one event that has already happened to the arc log; never put a future plan here"), false),
            ("state", "string", L("Статус: active / done / abandoned (опционально)", "Status: active / done / abandoned (optional)"), false)),
        Def("buy_rumors",
            L("Покупка набора слухов в трактире или лавке. Один слух обязательно связывается с активной аркой или принятым заданием; для остальных программа честно крутит колесо: 65% малая зацепка, 27% средний квест, 8% длинный квест с хорошей наградой и возможным компаньоном. Списывает золото и сохраняет подсказки для последующего припоминания.",
              "Buying a set of rumors in a tavern or a shop. One rumor is always linked to the active arc or an accepted quest; for the rest the program fairly spins the wheel: 65% a small lead, 27% a medium quest, 8% a long quest with a good reward and a possible companion. Deducts the gold and saves the hints for later recall."),
            ("source", "string", L("Кто продал или рассказал слухи", "Who sold or told the rumors"), true),
            ("cost", "integer", L("Общая цена в золоте", "The total price in gold"), true),
            ("count", "integer", L("Количество слухов 2–5 (по умолчанию 3)", "Number of rumors 2–5 (default 3)"), false),
            ("linked_to", "string", L("Точное название текущей арки или принятого задания", "The exact title of the current arc or an accepted quest"), true),
            ("linked_clue", "string", L("Краткое точное содержание полезной подсказки", "The brief exact content of the useful hint"), true),
            ("minor_rumors", "string", L("Несколько малых зацепок через |: работа, ограбление, клад, странная покупка и т.п.", "Several small leads separated by |: a job, a robbery, a treasure, a strange purchase, etc."), true),
            ("medium_quests", "string", L("Несколько завязок средних/продолжительных квестов через |", "Several hooks of medium/extended quests separated by |"), true),
            ("long_quests", "string", L("Несколько завязок длинных квестов с хорошей наградой и возможным компаньоном через |", "Several hooks of long quests with a good reward and a possible companion separated by |"), true)),
        Def("recall_rumor",
            L("Вспомнить ранее купленную подсказку после того, как игрок выбрал соответствующий вариант. Возвращает мастеру точную запись; перескажи её расплывчато, как неполное воспоминание героя, а не цитатой.",
              "Recall a previously bought hint after the player picked the matching option. Returns the exact record to the master; retell it vaguely, as the hero's incomplete memory, not as a quote."),
            ("source", "string", L("Кто дал подсказку (если известно)", "Who gave the hint (if known)"), false),
            ("linked_to", "string", L("Арка/задание/зацепка, к которой относится воспоминание", "The arc/quest/lead the memory relates to"), false)),
        Def("update_party_member",
            L("Обновить самостоятельного участника группы (ХП, ману, состояния, характер). НОВОГО спутника добавляй через update_companion_candidate → ask_companion_role → recruit_companion: " +
            "так игрок выбирает роль, а харнес собирает сбалансированный лист. В группе максимум 3 персонажа. Они сами выбирают действия и навыки.",
            "Update an independent party member (HP, mana, conditions, personality). Add a NEW companion via update_companion_candidate → ask_companion_role → recruit_companion: " +
            "that way the player picks the role and the harness builds a balanced sheet. The party has at most 3 characters. They choose their actions and skills themselves."),
            ("name", "string", L("Имя", "Name"), true),
            ("race", "string", L("Раса", "Race"), false),
            ("class", "string", L("Класс", "Class"), false),
            ("gender", "string", L("Пол", "Gender"), false),
            ("level", "string", L("Уровень", "Level"), false),
            ("hp_current", "integer", L("Текущее HP", "Current HP"), false),
            ("hp_delta", "integer", L("Изменение HP: отрицательное = урон, положительное = лечение; щит поглощает урон первым. В бою предпочитай его абсолютному hp_current — так журнал боя покажет, сколько снято или добавлено", "HP change: negative = damage, positive = healing; the shield absorbs damage first. In combat prefer it to the absolute hp_current — that way the battle log shows how much was taken or added"), false),
            ("hp_max", "integer", L("Максимальное HP", "Maximum HP"), false),
            ("mana_current", "integer", L("Текущая мана", "Current mana"), false),
            ("mana_max", "integer", L("Максимальная мана", "Maximum mana"), false),
            ("shield", "integer", L("Временный щит", "Temporary shield"), false),
            ("add_status", "string", L("Добавить состояние (отравление, благословение…) (опционально). Повторное — обновит длительность", "Add a condition (poison, blessing…) (optional). Repeating it refreshes the duration"), false),
            ("rounds", "integer", L("Длительность add_status в раундах (в бою по умолчанию — типичная для эффекта)", "Duration of add_status in rounds (in combat the default is typical for the effect)"), false),
            ("remove_status", "string", L("Снять состояние (опционально)", "Remove a condition (optional)"), false),
            ("source", "string", L("Кто вызвал изменение — для журнала боя: «Гоблин 1» при уроне, «Лира» при лечении (опционально)", "Who caused the change — for the battle log: \"Goblin 1\" for damage, \"Lyra\" for healing (optional)"), false),
            ("portrait", "string", L("id облика строго по расе и полу: ", "A look id strictly by race and gender: ") + PortraitCatalog.PersonPromptList(), false),
            ("role", "string", L("Роль в группе: ", "Role in the party: ") + string.Join(", ", Progression.Archetypes.Select(a => $"{a.Key} ({a.Title.ToLowerInvariant()})")), false),
            ("str", "integer", L("Сила 1–30", "Strength 1–30"), false),
            ("dex", "integer", L("Ловкость 1–30", "Dexterity 1–30"), false),
            ("con", "integer", L("Телосложение 1–30", "Constitution 1–30"), false),
            ("int", "integer", L("Интеллект 1–30", "Intelligence 1–30"), false),
            ("wis", "integer", L("Мудрость 1–30", "Wisdom 1–30"), false),
            ("cha", "integer", L("Харизма 1–30", "Charisma 1–30"), false),
            ("skill", "string", L("Добавить навык/умение", "Add a skill/ability"), false),
            ("note", "string", L("Характер, цели, отношение к группе", "Personality, goals, attitude to the party"), false),
            ("remove", "boolean", L("Удалить из группы только после отыгранного разговора об уходе", "Remove from the party only after a played-out conversation about leaving"), false),
            ("departure", "string", L("При remove: normal / hostile / dead. normal переносит бывшего спутника в важных персонажей", "With remove: normal / hostile / dead. normal moves the former companion to the important characters"), false)),
        Def("update_skill",
            L("Выучить, развить, изменить или забыть навык героя/компаньона. У каждого максимум 8 навыков; если места нет, сначала вызови remove=true для выбранного навыка. " +
            "Эволюция усиливает существующий навык, а не занимает новый слот. Ступень ограничена уровнем владельца (1 на 1–4, 2 на 5–8, 3 на 9–12, 4 на 13–16, 5 с 17-го). " +
            "Урон, лечение и щиты пиши в костях мощи К (К = кость мощи владельца, растёт с уровнем): ",
            "Learn, develop, change or forget a skill of the hero/a companion. Each has at most 8 skills; if there is no room, first call remove=true for the chosen skill. " +
            "Evolution strengthens an existing skill instead of taking a new slot. The tier is limited by the owner's level (1 at 1–4, 2 at 5–8, 3 at 9–12, 4 at 13–16, 5 from 17th). " +
            "Write damage, healing and shields in power dice K (K = the owner's power die, grows with level): ") +
            string.Join("; ", Enumerable.Range(1, 5).Select(r => L($"ступень {r} — ", $"tier {r} — ") + Progression.SkillBudget(r))) + L(". Пример: «1.5К огнём + поджог».", ". Example: \"1.5K fire + burning\"."),
            ("owner", "string", L("hero или точное имя участника группы", "hero or the exact name of a party member"), true),
            ("name", "string", L("Название навыка", "The skill name"), true),
            ("category", "string", "attack / support / heal / cleanse / shield / revive / debuff / utility", false),
            ("rank", "integer", L("Ступень эволюции 1–5", "Evolution tier 1–5"), false),
            ("mana_cost", "integer", L("Стоимость маны", "Mana cost"), false),
            ("target", "string", "self / ally / enemy / all-allies / all-enemies", false),
            ("description", "string", L("Точный игровой эффект", "The exact game effect"), false),
            ("icon", "string", L("id иконки по смыслу навыка (не указан — подберётся по названию): ", "An icon id by the skill's meaning (if not given, picked by name): ") + SkillIcons.PromptList(), false),
            ("evolution", "string", L("Что вызвало и как проявилась эволюция", "What caused the evolution and how it showed"), false),
            ("beyond_level", "boolean", L("Сюжетное исключение: ступень выше уровня владельца (легендарный наставник, дар бога) — крайне редко", "A story exception: a tier above the owner's level (a legendary mentor, a god's gift) — extremely rare"), false),
            ("remove", "boolean", L("Забыть навык и освободить слот", "Forget the skill and free the slot"), false)),
        Def("update_party_inventory",
            L("Изменить видимый инвентарь участника группы. Передача вещи между героем и участником разрешена только после его явного согласия в сцене; тогда consent=true.",
              "Change the visible inventory of a party member. Passing an item between the hero and a member is allowed only after their explicit consent in the scene; then consent=true."),
            ("member", "string", L("Имя участника группы", "The party member's name"), true),
            ("item", "string", L("Название предмета", "The item name"), true),
            ("quantity", "integer", L("Новое количество у участника; 0 — убрать", "The member's new quantity; 0 — remove"), true),
            ("icon", "string", L("id иконки каталога", "A catalog icon id"), false),
            ("note", "string", L("Описание/назначение", "Description/purpose"), false),
            ("consent", "boolean", L("Участник явно согласился на передачу", "The member explicitly agreed to the transfer"), true)),
        Def("transfer_party_item",
            L("Атомарно передать существующий предмет между сумкой героя и сумкой спутника после отыгранного согласия. Инструмент сам проверяет место и не допускает дублирования вещи.",
              "Atomically move an existing item between the hero's bag and a companion's bag after played-out consent. The tool checks the space itself and does not allow duplicating the item."),
            ("member", "string", L("Имя участника группы", "The party member's name"), true),
            ("direction", "string", "hero_to_party / party_to_hero", true),
            ("item_id", "string", L("Точный id из служебного запроса интерфейса, если указан", "The exact id from the interface's service request, if given"), false),
            ("item", "string", L("Название предмета, если id неизвестен", "The item name if the id is unknown"), false),
            ("quantity", "integer", L("Количество передаваемых единиц; по умолчанию 1", "Number of units to transfer; default 1"), false),
            ("consent", "boolean", L("Спутник явно согласился взять или отдать предмет", "The companion explicitly agreed to take or give the item"), true)),
        Def("consume_item",
            L("Списать расходник после того, как его применение и эффект уже разрешены. Эффект на HP, ману, щит или состояния отдельно зафиксируй update_character/update_party_member.",
              "Consume a consumable after its use and effect are already resolved. Record the effect on HP, mana, shield or conditions separately with update_character/update_party_member."),
            ("owner", "string", L("hero или имя спутника, в чьей сумке лежит расходник", "hero or the name of the companion whose bag holds the consumable"), true),
            ("item_id", "string", L("Точный id из служебного запроса интерфейса, если указан", "The exact id from the interface's service request, if given"), false),
            ("item", "string", L("Название расходника, если id неизвестен", "The consumable's name if the id is unknown"), false),
            ("quantity", "integer", L("Сколько единиц израсходовать; по умолчанию 1", "How many units to consume; default 1"), false),
            ("consent", "boolean", L("Для вещи спутника: он согласился её использовать", "For a companion's item: they agreed to use it"), false)),
        Def("update_world_event",
            L("Создать или обновить глобальное событие мира (война, стихия, праздник, проклятие), влияющее на сюжет.",
              "Create or update a global world event (a war, a disaster, a festival, a curse) that affects the story."),
            ("name", "string", L("Название события", "The event name"), true),
            ("description", "string", L("Что происходит и на что влияет (опционально)", "What is happening and what it affects (optional)"), false),
            ("state", "string", L("Статус: ongoing / upcoming / ended (опционально)", "Status: ongoing / upcoming / ended (optional)"), false),
            ("impact", "string", L("Как событие может коснуться героя (опционально)", "How the event may touch the hero (optional)"), false)),
        Def("set_world_state",
            L("Обновить сводку текущего состояния мира: где сейчас герой, дата/время суток, погода, чем занят, что происходит вокруг. 2–4 предложения.",
              "Update the summary of the current world state: where the hero is now, the date/time of day, the weather, what they are doing, what is happening around. 2–4 sentences."),
            ("text", "string", L("Новая сводка (полностью заменяет старую); можно не передавать, если меняешь только время", "The new summary (fully replaces the old one); can be omitted if you only change the time"), false),
            ("day_part", "string", L("Часть суток теперь: утро / день / вечер / ночь (более ранняя, чем сейчас, — уже следующий день)", "The part of day now: morning / day / evening / night (an earlier one than now means the next day)"), false),
            ("days_passed", "integer", L("Сколько дней прошло (ожидание, лечение, путешествие без travel) — часы угрозы тикают сами", "How many days passed (waiting, healing, a journey without travel) — threat clocks tick on their own"), false),
            ("place", "string", L("Герой теперь здесь (город, лес, данж, дорога…) — меняет место сцены: прежние торговцы и жители уходят из «Взаимодействия неподалёку» до возвращения (опционально)", "The hero is now here (a town, a forest, a dungeon, a road…) — changes the scene place: the previous merchants and residents leave \"Nearby interactions\" until the hero returns (optional)"), false)),
        Def("equip_item",
            L("Надеть или снять предмет. Если предмет с таким названием лежит в сумке — он переносится из сумки на героя; " +
            "надетое ранее в этом слоте возвращается в сумку. unequip=true — снять в сумку. " +
            "Слоты: Helmet, Body, Cloak, Gloves, Boots, Belt, Amulet, Ring1, Ring2, Hand1 (оружие), Hand2 (щит или второе оружие), TwoHanded (двуручное — обе руки).",
            "Put on or take off an item. If an item with that name lies in the bag, it is moved from the bag onto the hero; " +
            "whatever was worn in that slot before returns to the bag. unequip=true — take off into the bag. " +
            "Slots: Helmet, Body, Cloak, Gloves, Boots, Belt, Amulet, Ring1, Ring2, Hand1 (weapon), Hand2 (a shield or a second weapon), TwoHanded (two-handed — both hands)."),
            ("slot", "string", L("Имя слота (для двуручного оружия — TwoHanded или Hand1 с two_handed=true)", "The slot name (for a two-handed weapon — TwoHanded or Hand1 with two_handed=true)"), true),
            ("name", "string", L("Название предмета (не нужно при unequip)", "The item name (not needed with unequip)"), false),
            ("icon", "string", L("id иконки из каталога (см. update_grid_item). Если не указан — подбирается по названию", "An icon id from the catalog (see update_grid_item). If not given, picked by name"), false),
            ("note", "string", L("Описание и история вещи (механику — в damage/armor/bonuses/effects) (опционально)", "The item's description and history (mechanics go into damage/armor/bonuses/effects) (optional)"), false),
            ("two_handed", "boolean", L("Двуручное оружие — занимает обе руки", "A two-handed weapon — takes both hands"), false),
            ("level", "integer", L("Уровень предмета (по умолчанию уровень героя)", "Item level (default — the hero's level)"), false),
            ("rarity", "string", RarityParamHelp, false),
            ("damage", "string", DamageParamHelp, false),
            ("armor", "integer", ArmorParamHelp, false),
            ("bonuses", "string", BonusesParamHelp, false),
            ("effects", "string", EffectsParamHelp, false),
            ("unequip", "boolean", L("Снять предмет со слота в сумку", "Take the item off the slot into the bag"), false),
            ("drop", "boolean", L("Вместе с unequip: снять и выбросить (не класть в сумку)", "Together with unequip: take off and throw away (not into the bag)"), false)),
        Def("update_grid_item",
            L($"Добавить предмет в сумку-сетку ({RpgState.GridCols} колонок x {RpgState.GridRows} строк), обновить или убрать его. " +
            "Ищется по названию (без регистра). Размер в клетках берётся из каталога иконки; w/h передавай, только если предмет необычного размера. " +
            "Стакающиеся мелочи (зелья, стрелы, еда) — одним предметом с quantity. " +
            "Каталог иконок (размер и слот подставляются сами; ★ — именной артефакт для эпических и легендарных вещей): ",
            $"Add an item to the grid bag ({RpgState.GridCols} columns x {RpgState.GridRows} rows), update or remove it. " +
            "Looked up by name (case-insensitive). The size in cells comes from the icon catalog; pass w/h only if the item has an unusual size. " +
            "Stackable small things (potions, arrows, food) — as one item with quantity. " +
            "Icon catalog (size and slot are filled in automatically; ★ — a named artifact for epic and legendary items): ") + _catalog.PromptList() + " " + ItemEconomy.PotionHints(),
            ("name", "string", L("Название предмета, как его видит игрок", "The item name as the player sees it"), true),
            ("icon", "string", L("id иконки из каталога; если не указан — подбирается по названию", "An icon id from the catalog; if not given, picked by name"), false),
            ("quantity", "integer", L("Количество (опционально)", "Quantity (optional)"), false),
            ("note", "string", L("Описание, история, квестовое назначение (механику — в damage/armor/bonuses/effects) (опционально)", "Description, history, quest purpose (mechanics go into damage/armor/bonuses/effects) (optional)"), false),
            ("w", "integer", L("Ширина в клетках 1–4 (по умолчанию из каталога)", "Width in cells 1–4 (default from the catalog)"), false),
            ("h", "integer", L("Высота в клетках 1–4 (по умолчанию из каталога)", "Height in cells 1–4 (default from the catalog)"), false),
            ("col", "integer", L($"Колонка 0–{RpgState.GridCols - 1} (опционально; иначе первое свободное место)", $"Column 0–{RpgState.GridCols - 1} (optional; otherwise the first free spot)"), false),
            ("row", "integer", L($"Строка 0–{RpgState.GridRows - 1} (опционально)", $"Row 0–{RpgState.GridRows - 1} (optional)"), false),
            ("slot", "string", L("Слот, если предмет можно надеть и его нет в каталоге (опционально)", "The slot, if the item can be worn and is not in the catalog (optional)"), false),
            ("two_handed", "boolean", L("Двуручное оружие (опционально)", "A two-handed weapon (optional)"), false),
            ("level", "integer", L("Уровень предмета (по умолчанию уровень героя): бюджет бонусов +10% за уровень, потолок атаки и КБ всегда +3", "Item level (default — the hero's level): the bonus budget +10% per level, the attack and AC cap is always +3"), false),
            ("consumable", "boolean", L("Расходуется при применении. Для зелий, еды, свитков и книг определяется автоматически", "Consumed on use. Detected automatically for potions, food, scrolls and books"), false),
            ("rarity", "string", RarityParamHelp, false),
            ("damage", "string", DamageParamHelp, false),
            ("armor", "integer", ArmorParamHelp, false),
            ("bonuses", "string", BonusesParamHelp, false),
            ("effects", "string", EffectsParamHelp, false),
            ("remove", "boolean", L("Удалить предмет (потрачен, продан, потерян)", "Remove the item (spent, sold, lost)"), false)),
    };

    private static JsonObject OfferChoicesDef() => RawDef(
        "offer_choices",
        L("Показать игроку кнопки быстрого ответа под твоим сообщением (варианты действий, ответы на вопрос сессии ноль). " +
        "Сам вопрос и описание ситуации пиши в тексте ответа; после вызова ход заканчивается — жди выбора игрока. " +
        "Игрок всегда может ответить своими словами.",
        "Show the player quick-answer buttons under your message (action options, answers to a session zero question). " +
        "Write the question itself and the description of the situation in the reply text; after the call the turn ends — wait for the player's choice. " +
        "The player can always answer in their own words."),
        new JsonObject
        {
            ["question"] = new JsonObject { ["type"] = "string", ["description"] = L("Короткая подпись над кнопками: «Какой мир?», «Что делаешь?»", "A short caption above the buttons: \"What world?\", \"What do you do?\"") },
            ["options"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = L("2–6 вариантов, каждый — ПРОСТАЯ СТРОКА (не объект) от лица игрока, до 60 символов; эмодзи можно поставить в начало строки", "2–6 options, each a PLAIN STRING (not an object) in the player's voice, up to 60 characters; an emoji may start the string"),
                ["items"] = new JsonObject { ["type"] = "string" },
            },
        },
        "question", "options");

    private static JsonObject ProposeArcsDef() => RawDef(
        "propose_arcs",
        L("Предложить игроку 3–5 сюжетных арок карточками с кнопкой выбора (и полем «свой вариант»). Каждая карточка содержит только название и завязку без спойлеров. " +
        "Вызывай после создания мира. После вызова ход заканчивается — жди выбора; выбранную арку сохрани через update_arc.",
        "Offer the player 3–5 story arcs as cards with a pick button (and a \"your own idea\" field). Each card contains only a title and a spoiler-free setup. " +
        "Call it after creating the world. After the call the turn ends — wait for the choice; save the chosen arc via update_arc."),
        new JsonObject
        {
            ["arcs"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = L("3–5 арок, заметно разных по исходной ситуации и жанру", "3–5 arcs, clearly different in their initial situation and genre"),
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["name"] = new JsonObject { ["type"] = "string", ["description"] = L("Название (до 6 слов)", "Title (up to 6 words)") },
                        ["premise"] = new JsonObject { ["type"] = "string", ["description"] = L("Завязка в 2–3 предложениях: только известная герою исходная ситуация и уже произошедшие события. Не раскрывай будущие этапы, антагониста, тайны, повороты, развязку и награды", "The setup in 2–3 sentences: only the initial situation known to the hero and events that have already happened. Do not reveal future stages, the antagonist, secrets, twists, resolution or rewards") },
                    },
                    ["required"] = new JsonArray("name", "premise"),
                },
            },
        },
        "arcs");

    private static JsonObject RawDef(string name, string description, JsonObject properties, params string[] required)
    {
        var req = new JsonArray();
        foreach (var r in required)
        {
            req.Add(r);
        }

        return new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = name,
                ["description"] = description,
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = req,
                },
            },
        };
    }

    private static JsonObject Def(string name, string description, params (string Name, string Type, string Desc, bool Req)[] props)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        foreach (var p in props)
        {
            properties[p.Name] = new JsonObject { ["type"] = p.Type, ["description"] = p.Desc };
            if (p.Req)
            {
                required.Add(p.Name);
            }
        }

        return new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = name,
                ["description"] = description,
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = required,
                },
            },
        };
    }

    /// <summary>Выполняет инструмент. Возвращает (успех, текст результата — он же ответ модели).
    /// workDir — папка кампании; chatId — текущий чат для RPG-инструментов (может быть null).</summary>
    public (bool Ok, string Result) Execute(string workDir, string name, string argumentsJson, string? chatId)
    {
        var (ok, result) = ExecuteCore(workDir, name, argumentsJson, chatId);

        // В киберпанке результаты механики говорят эдди, ОЗУ и фиксерами. Содержимое файлов кампании не трогаем.
        return name is "read_file" or "list_files" or "create_file" or "edit_file" ? (ok, result) : (ok, Genre.Localize(result));
    }

    private (bool Ok, string Result) ExecuteCore(string workDir, string name, string argumentsJson, string? chatId)
    {
        JsonObject? args;
        try
        {
            // События журнала живут ровно один вызов: остатки прошлого (если журнал их не забрал) не должны всплыть.
            if (chatId is not null) CombatEvents.Drain(chatId);
            args = JsonNode.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson) as JsonObject;
            if (args is null)
            {
                return (false, L("Ошибка: аргументы не являются JSON-объектом.", "Error: the arguments are not a JSON object."));
            }
        }
        catch (JsonException)
        {
            return (false, L(
                "Ошибка: модель прислала оборванные или некорректные JSON-аргументы инструмента. Повтори весь вызов инструмента заново; сократи длинные поля и не продолжай оборванный JSON.",
                "Error: the model sent truncated or invalid JSON tool arguments. Repeat the entire tool call from scratch; shorten long fields and do not continue the truncated JSON."));
        }

        try
        {
            return name switch
            {
                "create_file" => Create(workDir, Arg(args, "path"), Arg(args, "content") ?? "", OptBool(args, "overwrite")),
                "edit_file" => Edit(workDir, Arg(args, "path"), Arg(args, "find") ?? "", Arg(args, "replace") ?? "", Arg(args, "append"), OptBool(args, "all")),
                "read_file" => Read(workDir, Arg(args, "path"), OptInt(args, "max_chars", DefaultReadChars)),
                "list_files" => List(workDir, Arg(args, "path") ?? ".", OptBool(args, "recursive")),
                "roll_dice" => RollAndRecord(chatId, args),
                "initiative" => InitiativeTool(chatId, args),
                "combat_turn" => CombatTurn(chatId, args),
                "resolve_death_save" => ResolveDeathSave(chatId, args),
                "travel" => Travel(chatId, args),
                "street_offer" => MakeStreetOffer(chatId, args),
                "street_deal" => CloseStreetDeal(chatId, args),
                "start_instance" => StartInstance(chatId, args),
                "finish_instance" => FinishInstance(chatId, args),
                "offer_choices" => OfferChoices(args),
                "propose_arcs" => ProposeArcs(args),
                "update_character" => UpdateCharacter(chatId, args),
                "set_stat" => SetStat(chatId, args),
                "update_inventory" => UpdateInventoryLegacy(chatId, args),
                "update_npc" => UpdateNpc(chatId, args),
                "update_adversary" => UpdateAdversary(chatId, args),
                "update_battle_loot" => UpdateBattleLoot(chatId, args),
                "update_city" => UpdateCity(chatId, args),
                "update_guild_job" => UpdateGuildJob(chatId, args),
                "update_guild_reputation" => UpdateGuildReputation(chatId, args),
                "open_adventure_guild" => OpenAdventureGuild(chatId, args),
                "update_quest" => UpdateQuest(chatId, args),
                "update_arc" => UpdateArc(chatId, args),
                "buy_rumors" => BuyRumors(chatId, args),
                "recall_rumor" => RecallRumor(chatId, args),
                "update_party_member" => UpdatePartyMember(chatId, args),
                "update_skill" => UpdateSkill(chatId, args),
                "update_party_inventory" => UpdatePartyInventory(chatId, args),
                "transfer_party_item" => TransferPartyItem(chatId, args),
                "consume_item" => ConsumeItem(chatId, args),
                "update_world_event" => UpdateWorldEvent(chatId, args),
                "set_world_state" => SetWorldState(chatId, args),
                "equip_item" => EquipItem(chatId, args),
                "update_grid_item" => UpdateGridItem(chatId, args),
                "set_merchant" => SetMerchant(chatId, args),
                "update_shop_item" => UpdateShopItem(chatId, args),
                "open_trade" => OpenTrade(chatId, args),
                "plan_encounter" => PlanEncounter(chatId, args),
                "award_xp" => AwardXp(chatId, args),
                "level_up" => LevelUp(chatId, args),
                "roll_loot" => RollLoot(chatId, args),
                "update_companion_candidate" => UpdateCompanionCandidate(chatId, args),
                "ask_companion_role" => AskCompanionRole(chatId, args),
                "recruit_companion" => RecruitCompanion(chatId, args),
                "resolve_attack" => ResolveAttack(chatId, args),
                "skill_check" => SkillCheck(chatId, args),
                "rest" => Rest(chatId, args),
                "update_clock" => UpdateClock(chatId, args),
                "update_faction" => UpdateFaction(chatId, args),
                _ => (false, L($"Ошибка: неизвестный инструмент «{name}».", $"Error: unknown tool \"{name}\".")),
            };
        }
        catch (Exception ex)
        {
            return (false, L("Ошибка: ", "Error: ") + ex.Message);
        }
    }

    private static (bool Ok, string Result) Create(string workDir, string? relPath, string content, bool overwrite)
    {
        var path = ResolveSafe(workDir, relPath);
        if (path is null)
        {
            return (false, L("Ошибка: некорректный путь — нужен относительный путь внутри папки кампании.", "Error: invalid path — a relative path inside the campaign folder is required."));
        }

        var template = File.Exists(path) && CampaignTemplates.IsUnfilled(path, File.ReadAllText(path));
        if (File.Exists(path) && !overwrite && !template)
        {
            return (false, L($"Ошибка: файл уже существует: {Rel(workDir, path)}. Передайте overwrite=true, чтобы перезаписать его, " +
                           "или дополни его через edit_file.",
                           $"Error: the file already exists: {Rel(workDir, path)}. Pass overwrite=true to overwrite it, " +
                           "or add to it via edit_file."));
        }

        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        File.WriteAllText(path, content);
        var verb = template ? L("заполнен шаблон", "filled the template") : overwrite ? L("перезаписан", "overwritten") : L("создан", "created");
        return (true, $"OK: {verb} {Rel(workDir, path)} ({ByteSize(content.Length)}).");
    }

    private static (bool Ok, string Result) Edit(string workDir, string? relPath, string find, string replace, string? append, bool all)
    {
        var path = ResolveSafe(workDir, relPath);
        if (path is null)
        {
            return (false, L("Ошибка: некорректный путь — нужен относительный путь внутри папки кампании.", "Error: invalid path — a relative path inside the campaign folder is required."));
        }

        // Дописывание в конец (журнал, хроника): файл создаётся при необходимости.
        if (!string.IsNullOrEmpty(append) && find.Length == 0)
        {
            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            var existing = File.Exists(path) ? File.ReadAllText(path) : "";
            var sep = existing.Length == 0 || existing.EndsWith('\n') ? "" : "\n";
            File.AppendAllText(path, sep + append.TrimEnd() + "\n");
            return (true, L($"OK: дописано в {Rel(workDir, path)}.", $"OK: appended to {Rel(workDir, path)}."));
        }

        if (find.Length == 0)
        {
            return (false, L("Ошибка: передай find (что заменить) или append (что дописать в конец).", "Error: pass find (what to replace) or append (what to add to the end)."));
        }

        if (!File.Exists(path))
        {
            return (false, L($"Ошибка: файл не найден: {Rel(workDir, path)}. Сначала создайте его через create_file.", $"Error: file not found: {Rel(workDir, path)}. Create it via create_file first."));
        }

        var text = File.ReadAllText(path);
        var count = CountOccurrences(text, find);
        if (count == 0)
        {
            return (false, L(
                "Ошибка: фрагмент find не найден в файле. Сначала вызови read_file для актуального содержимого, затем повтори edit_file с коротким точным фрагментом (регистр и переводы строк важны). Не останавливай ход на этой ошибке.",
                "Error: the find fragment was not found in the file. Call read_file for the current contents first, then retry edit_file with a short exact fragment (case and line breaks matter). Do not stop the turn on this error."));
        }

        text = all ? text.Replace(find, replace) : ReplaceFirst(text, find, replace);
        File.WriteAllText(path, text);
        return (true, L($"OK: заменено вхождений: {(all ? count : 1)} в {Rel(workDir, path)}.", $"OK: replaced occurrences: {(all ? count : 1)} in {Rel(workDir, path)}."));
    }

    private static (bool Ok, string Result) Read(string workDir, string? relPath, int maxChars)
    {
        var path = ResolveSafe(workDir, relPath);
        if (path is null)
        {
            return (false, L("Ошибка: некорректный путь — нужен относительный путь внутри папки кампании.", "Error: invalid path — a relative path inside the campaign folder is required."));
        }

        if (!File.Exists(path))
        {
            return (false, L($"Ошибка: файл не найден: {Rel(workDir, path)}.", $"Error: file not found: {Rel(workDir, path)}."));
        }

        maxChars = maxChars <= 0 ? DefaultReadChars : Math.Min(maxChars, MaxReadChars);
        var text = File.ReadAllText(path);

        if (LooksBinary(text))
        {
            return (false, L("Ошибка: файл выглядит двоичным и не читается как текст.", "Error: the file looks binary and cannot be read as text."));
        }

        var suffix = text.Length > maxChars ? L($"\n…(показано {maxChars} из {text.Length} символов)", $"\n…(showing {maxChars} of {text.Length} characters)") : "";
        return (true, text[..Math.Min(text.Length, maxChars)] + suffix);
    }

    private static (bool Ok, string Result) List(string workDir, string? relPath, bool recursive)
    {
        var dirPath = ResolveSafe(workDir, relPath);
        if (dirPath is null)
        {
            return (false, L("Ошибка: некорректный путь — нужен относительный путь внутри папки кампании.", "Error: invalid path — a relative path inside the campaign folder is required."));
        }

        if (!Directory.Exists(dirPath))
        {
            return (false, L($"Ошибка: папка не найдена: {Rel(workDir, dirPath)}.", $"Error: folder not found: {Rel(workDir, dirPath)}."));
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        };

        var sb = new StringBuilder();
        var count = 0;
        foreach (var entry in Directory.EnumerateFileSystemEntries(dirPath, "*", options))
        {
            if (++count > MaxListEntries)
            {
                sb.Append(L("…(список обрезан)", "…(list truncated)"));
                break;
            }

            if (Directory.Exists(entry))
            {
                sb.Append(Rel(workDir, entry)).Append("/\n");
            }
            else
            {
                sb.Append(Rel(workDir, entry)).Append(" (").Append(ByteSize(new FileInfo(entry).Length)).Append(")\n");
            }
        }

        if (count == 0)
        {
            return (true, L("Папка пуста.", "The folder is empty."));
        }

        return (true, sb.ToString());
    }

    // ===== RPG-инструменты (состояние панели «Книга героя») =====

    /// <summary>RPG-инструменты требуют привязки к чату.</summary>
    private static readonly string[] RpgTools =
    {
        "start_instance", "finish_instance", "update_character", "set_stat", "update_inventory", "update_npc", "update_adversary", "update_battle_loot",
        "update_city", "update_guild_job", "update_guild_reputation", "open_adventure_guild", "update_quest", "update_arc", "buy_rumors", "recall_rumor", "update_party_member", "update_skill", "update_party_inventory", "transfer_party_item", "consume_item", "update_world_event", "set_world_state",
        "equip_item", "update_grid_item", "set_merchant", "update_shop_item", "open_trade", "initiative", "combat_turn", "resolve_death_save", "street_offer", "street_deal"
    };

    public static bool IsRpgTool(string name) => RpgTools.Contains(name) || ProgressionTools.Contains(name) || TacticsTools.Contains(name);

    private (bool Ok, string Result) StartInstance(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L(L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."), "Error: the tool is available only inside an open chat."));
        if (!_settings.Current.SeparateInstanceSessions)
            return (false, L("Ошибка: раздельные сессии выключены в настройках. Веди сцену в основном контексте.", "Error: separate sessions are turned off in the settings. Run the scene in the main context."));

        var state = _rpg.GetOrCreate(chatId);
        if (state.ActiveInstance is { Active: true } current)
            return (false, L($"Ошибка: уже идёт изолированная сцена «{current.Title}». Вложенные инстансы запрещены.", $"Error: an isolated scene \"{current.Title}\" is already running. Nested instances are forbidden."));

        var kind = (Arg(args, "kind") ?? "other").Trim().ToLowerInvariant();
        if (kind is not ("dungeon" or "combat" or "chase" or "infiltration" or "trial" or "other")) kind = "other";
        var title = (Arg(args, "title") ?? "").Trim();
        var brief = (Arg(args, "brief") ?? "").Trim();
        var exit = (Arg(args, "exit_condition") ?? "").Trim();
        if (title.Length == 0 || brief.Length == 0 || exit.Length == 0)
            return (false, L("Ошибка: нужны title, brief и exit_condition.", "Error: title, brief and exit_condition are required."));

        var instance = new InstanceSession
        {
            Kind = kind,
            Title = title,
            Brief = brief,
            ExitCondition = exit,
            PrevScenePlace = state.ScenePlace ?? "",
        };
        instance.Messages.Add(new ChatMessage("user", L(
            $"[СЛУЖЕБНАЯ ПЕРЕДАЧА ОТ ОСНОВНОГО МАСТЕРА — игрок это сообщение не видит]\n" +
            $"Ты — суб-мастер изолированной сцены «{title}» (тип: {kind}). Проведи только эту сцену, сохраняя стиль кампании. " +
            "Ты видишь актуальную Книгу героя и можешь читать любые .md-файлы кампании через read_file/list_files. " +
            "Все изменения состояния немедленно вноси обычными инструментами. Не начинай новую сюжетную арку и не переписывай факты мира. " +
            $"Когда выполнено условие выхода, сначала зафиксируй изменения, затем вызови finish_instance.\n\n" +
            $"БРИФ:\n{brief}\n\nУСЛОВИЕ ВЫХОДА:\n{exit}",
            $"[SERVICE HANDOVER FROM THE MAIN MASTER — the player does not see this message]\n" +
            $"You are the sub-master of the isolated scene \"{title}\" (type: {kind}). Run only this scene, keeping the campaign's style. Write in English. " +
            "You see the current Hero book and can read any campaign .md files via read_file/list_files. " +
            "Record all state changes immediately with the usual tools. Do not start a new story arc and do not rewrite the facts of the world. " +
            $"When the exit condition is met, first record the changes, then call finish_instance.\n\n" +
            $"BRIEF:\n{brief}\n\nEXIT CONDITION:\n{exit}")) { Hidden = true, InstanceId = instance.Id });
        state.ActiveInstance = instance;
        // В замкнутой сцене прежние горожане и торговцы не рядом — место сцены меняется на неё,
        // при finish_instance вернётся PrevScenePlace и они снова появятся.
        state.ScenePlace = title;
        _rpg.Save(chatId, state);
        return (true, L($"OK: сцена «{title}» передана суб-мастеру (instance {instance.Id}). Следующий раунд ведётся в отдельном контексте; не пересказывай бриф игроку.", $"OK: the scene \"{title}\" is handed to the sub-master (instance {instance.Id}). The next round runs in a separate context; do not retell the brief to the player."));
    }

    private (bool Ok, string Result) FinishInstance(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L(L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."), "Error: the tool is available only inside an open chat."));
        var state = _rpg.GetOrCreate(chatId);
        var instance = state.ActiveInstance;
        if (instance is not { Active: true }) return (false, L("Ошибка: активной изолированной сцены нет.", "Error: there is no active isolated scene."));
        var summary = (Arg(args, "summary") ?? "").Trim();
        if (summary.Length == 0) return (false, L("Ошибка: нужна краткая итоговая сводка (summary).", "Error: a brief final summary (summary) is required."));

        instance.Active = false;
        // Возврат на прежнее место: соседние персонажи снова становятся доступны.
        state.ScenePlace = instance.PrevScenePlace ?? "";
        var handoff = new InstanceHandoff
        {
            Id = instance.Id,
            Kind = instance.Kind,
            Title = instance.Title,
            Summary = summary,
            Relationships = (Arg(args, "relationships") ?? "").Trim(),
            Loot = (Arg(args, "loot") ?? "").Trim(),
            Resources = (Arg(args, "resources") ?? "").Trim(),
            Consequences = (Arg(args, "consequences") ?? "").Trim(),
            OpenThreads = (Arg(args, "open_threads") ?? "").Trim(),
        };
        state.InstanceHandoffs.Add(handoff);
        if (state.InstanceHandoffs.Count > 8) state.InstanceHandoffs.RemoveRange(0, state.InstanceHandoffs.Count - 8);
        _rpg.Save(chatId, state);
        return (true, L($"OK: инстанс «{instance.Title}» завершён. Основной мастер снова активен. Итог: {summary}", $"OK: the instance \"{instance.Title}\" is finished. The main master is active again. Outcome: {summary}"));
    }

    /// <summary>Этап кампании: сессия ноль → выбор арки → приключение.</summary>
    public enum CampaignPhase { SessionZero, ArcChoice, Adventure }

    public CampaignPhase GetPhase(string? chatId)
    {
        var state = _rpg.GetOrCreate(chatId);
        if (state.StoryArcs.Any(a => a.State == "active"))
        {
            return CampaignPhase.Adventure;
        }

        return _rpg.IsWorldCreated(chatId) ? CampaignPhase.ArcChoice : CampaignPhase.SessionZero;
    }

    /// <summary>
    /// Сводка книги героя для системного промпта мастера: этап кампании, что уже известно,
    /// а при обрезанной истории — хвост журнала кампании.
    /// </summary>
    public string BuildStateNote(string? chatId, string? journalTail = null)
    {
        var state = _rpg.GetOrCreate(chatId);
        var c = state.Character;
        var sb = new StringBuilder();

        sb.AppendLine(L("# Текущее состояние кампании", "# Current campaign state"));
        sb.AppendLine(GetPhase(chatId) switch
        {
            CampaignPhase.SessionZero => L(
                "Этап: 1. СЕССИЯ НОЛЬ — мир ещё не создан (World.md — пустой шаблон). Расспроси игрока о мире и правилах, затем создай основу мира.",
                "Stage: 1. SESSION ZERO — the world is not created yet (World.md is an empty template). Ask the player about the world and the rules, then create the foundation of the world."),
            CampaignPhase.ArcChoice => L(
                "Этап: 2. ВЫБОР АРКИ — мир создан, активной арки нет. Предложи 3–5 арок через propose_arcs или сохрани выбранную игроком через update_arc (state=active).",
                "Stage: 2. CHOOSING AN ARC — the world is created, there is no active arc. Offer 3–5 arcs via propose_arcs or save the one the player chose via update_arc (state=active)."),
            _ => L(
                "Этап: 3. ПРИКЛЮЧЕНИЕ — веди игру по активной арке.",
                "Stage: 3. ADVENTURE — run the game along the active arc."),
        });

        if (state.ActiveInstance is { Active: true } instance)
        {
            sb.AppendLine();
            sb.AppendLine(L($"РЕЖИМ СУБ-МАСТЕРА: активна изолированная сцена «{instance.Title}» ({instance.Kind}, id {instance.Id}).", $"SUB-MASTER MODE: the isolated scene \"{instance.Title}\" is active ({instance.Kind}, id {instance.Id})."));
            sb.AppendLine(L("Веди только этот инстанс. Основная история чата скрыта от этого контекста; необходимые факты находятся в служебном брифе ниже по истории, текущей сводке и .md-файлах кампании.", "Run only this instance. The main chat history is hidden from this context; the necessary facts are in the service brief further down the history, the current summary and the campaign .md files."));
            sb.AppendLine(L("Условие возврата основному мастеру: ", "Condition for returning to the main master: ") + instance.ExitCondition);
            sb.AppendLine(L("Перед finish_instance сначала внеси все изменения HP, маны, инвентаря, заданий, NPC и мира соответствующими инструментами.", "Before finish_instance first record all changes of HP, mana, inventory, quests, NPCs and the world with the matching tools."));
        }
        else if (state.InstanceHandoffs.LastOrDefault() is { } handoff)
        {
            static string Part(string label, string value) => string.IsNullOrWhiteSpace(value) ? "" : $"\n- {label}: {value}";
            sb.AppendLine();
            sb.AppendLine(L($"Последняя передача от суб-мастера — «{handoff.Title}»: ", $"The last handover from the sub-master — \"{handoff.Title}\": ") + handoff.Summary +
                          Part(L("Отношения", "Relationships"), handoff.Relationships) + Part(L("Добыча", "Loot"), handoff.Loot) +
                          Part(L("Ресурсы", "Resources"), handoff.Resources) + Part(L("Последствия", "Consequences"), handoff.Consequences) +
                          Part(L("Незакрытые нити", "Loose threads"), handoff.OpenThreads));
            sb.AppendLine(L("Продолжай основную кампанию с учётом этой передачи, но не пересказывай игроку служебную сводку дословно.", "Continue the main campaign taking this handover into account, but do not retell the service summary to the player verbatim."));
        }

        static string Cut(string? s, int max)
        {
            var t = (s ?? "").Trim().Replace("\r", " ").Replace("\n", "; ");
            return t.Length <= max ? t : t[..max].TrimEnd() + "…";
        }

        sb.AppendLine();
        sb.AppendLine(L("Книга героя (правь только инструментами):", "Hero book (edit only with tools):"));
        var origin = string.Join(" ", new[] { c.Race, c.CharClass, c.Gender }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var who = string.IsNullOrWhiteSpace(c.Name) ? L("безымянный герой", "a nameless hero") : c.Name.Trim();
        var head = string.IsNullOrWhiteSpace(origin) ? who : $"{who}, {origin}";
        sb.Append(L($"- Герой: {head}, уровень {(string.IsNullOrWhiteSpace(c.Level) ? "1" : c.Level.Trim())}, ХП {c.HpCurrent}/{c.HpMax}",
                    $"- Hero: {head}, level {(string.IsNullOrWhiteSpace(c.Level) ? "1" : c.Level.Trim())}, HP {c.HpCurrent}/{c.HpMax}"));
        if (c.ManaMax > 0)
        {
            sb.Append(L($", мана {c.ManaCurrent}/{c.ManaMax}", $", mana {c.ManaCurrent}/{c.ManaMax}"));
        }

        sb.AppendLine(L($", щит {c.Shield}, золото {c.Gold}, гильдия {GuildRanks.TitleFor(c.GuildReputation)} ({c.GuildReputation} реп.).",
                        $", shield {c.Shield}, gold {c.Gold}, guild {GuildRanks.TitleFor(c.GuildReputation)} ({c.GuildReputation} rep.)."));
        if (c.HpCurrent <= 0)
        {
            sb.AppendLine(L($"  При смерти: {(c.DeathState.Length > 0 ? c.DeathState : "dying")}; успехи {c.DeathSaveSuccesses}/3, провалы {c.DeathSaveFailures}/3. " +
                          "Спасбросок делай только через resolve_death_save.",
                          $"  Dying: {(c.DeathState.Length > 0 ? c.DeathState : "dying")}; successes {c.DeathSaveSuccesses}/3, failures {c.DeathSaveFailures}/3. " +
                          "Make the saving throw only via resolve_death_save."));
        }
        var heroLevel = Progression.ParseLevel(c.Level);
        var heroArch = Progression.ArchetypeOf(c);
        var nextXp = Progression.XpFor(heroLevel + 1);
        sb.AppendLine(L($"  Прогрессия: опыт {c.Xp}/{nextXp} (уровень каждые {Progression.XpPerLevel}), архетип «{heroArch.Title}» ({heroArch.Key}), " +
                      $"кость мощи К = {Progression.PowerDice(heroLevel)}d8, макс. ступень навыков {Progression.MaxSkillRank(heroLevel)}." +
                      (c.Xp >= nextXp ? $" ⚠ Опыта хватает на уровень {heroLevel + 1} — вызови level_up." : ""),
                      $"  Progression: XP {c.Xp}/{nextXp} (a level every {Progression.XpPerLevel}), archetype \"{heroArch.Title}\" ({heroArch.Key}), " +
                      $"power die K = {Progression.PowerDice(heroLevel)}d8, max skill tier {Progression.MaxSkillRank(heroLevel)}." +
                      (c.Xp >= nextXp ? $" ⚠ Enough XP for level {heroLevel + 1} — call level_up." : "")));
        if (c.Skills.Count > 0) sb.AppendLine(L("  Навыки", "  Skills") + $" ({c.Skills.Count}/8): {string.Join("; ", c.Skills.Select(s => $"{s.Name} {L("ст.", "t.")}{s.Rank} [{s.Category}, {s.Target}, {s.ManaCost} {L("маны", "mana")}] — {Cut(Progression.ExplainPowerDice(s.Description, heroLevel), 120)}"))}.");
        // Характеристики с учётом надетого: «СИЛ 16 (+3; база 15, +1 от вещей)».
        var statText = Enum.GetValues<DndStat>().Select(st =>
        {
            var eff = ItemStats.Effective(c, st);
            var bonus = eff - c.Stat(st);
            return $"{DndStatNames.Short[(int)st]} {eff} ({DndStatNames.ModText(eff)}" +
                   (bonus != 0 ? L($"; база {c.Stat(st)}, {bonus:+#;-#} от вещей", $"; base {c.Stat(st)}, {bonus:+#;-#} from items") : "") + ")";
        });
        sb.AppendLine(L("  Характеристики (уже с вещами): ", "  Ability scores (items included): ") + string.Join(", ", statText) + ".");
        sb.AppendLine(L($"  Класс брони {ItemStats.ArmorClass(c)}, бонус мастерства +{ItemStats.Proficiency(c)}. Атака: {ItemStats.AttackLine(c)}.",
                        $"  Armor class {ItemStats.ArmorClass(c)}, proficiency bonus +{ItemStats.Proficiency(c)}. Attack: {ItemStats.AttackLine(c)}."));
        var effects = ItemStats.ActiveEffects(c);
        if (effects.Count > 0)
        {
            sb.AppendLine(L("  Свойства надетых вещей (действуют, учитывай их): ", "  Properties of worn items (active, take them into account): ") + string.Join("; ", effects.Select(e => $"{e.Effect} — {e.Item}")) + ".");
        }
        if (c.Status.Count > 0)
        {
            sb.AppendLine(L("  Состояния: ", "  Conditions: ") + string.Join(", ", c.Status) + ".");
        }

        if (!string.IsNullOrWhiteSpace(c.Notes))
        {
            sb.AppendLine(L("  Предыстория и заметки: ", "  Backstory and notes: ") + Cut(c.Notes, 600));
        }

        var equip = ItemStats.Worn(c)
            .Where(w => !string.IsNullOrWhiteSpace(w.Item.Name))
            .Select(w =>
            {
                var slots = w.Item.TwoHanded ? "Hand1+Hand2" : w.Slot;
                var stats = ItemStats.Describe(w.Item.Damage, w.Item.Armor, w.Item.Bonuses, new());
                var rarity = ItemEconomy.RarityKey(w.Item.Rarity) == "common" ? "" : $", {ItemEconomy.RarityTitle(w.Item.Rarity).ToLowerInvariant()}";
                return $"{w.Item.Name} [{slots}{rarity}]" +
                       (stats.Length > 0 ? $" {{{stats}}}" : L(" {без характеристик}", " {no stats}")) +
                       (string.IsNullOrWhiteSpace(w.Item.Note) ? "" : $" ({Cut(w.Item.Note, 60)})");
            })
            .ToList();
        sb.AppendLine(equip.Count > 0 ? L("- Надето: ", "- Worn: ") + string.Join("; ", equip) + "." : L("- Надето: ничего.", "- Worn: nothing."));

        var grid = state.Grid.Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .Select(i => $"{i.Name}{(i.Quantity > 1 ? $" x{i.Quantity}" : "")} {i.W}x{i.H}" +
                         (ItemStats.HasStats(i) ? $" {{{ItemStats.Describe(i)}}}" : "") +
                         (string.IsNullOrWhiteSpace(i.Note) ? "" : $" ({Cut(i.Note, 50)})"))
            .ToList();
        var used = state.Grid.Sum(i => i.W * i.H);
        sb.AppendLine(grid.Count > 0
            ? L($"- Сумка (занято {used}/{RpgState.GridCols * RpgState.GridRows} клеток): ", $"- Bag ({used}/{RpgState.GridCols * RpgState.GridRows} cells used): ") + string.Join("; ", grid) + "."
            : L("- Сумка: пуста.", "- Bag: empty."));
        sb.AppendLine(state.LegendaryTokens > 0
            ? L($"- Разрешений на легендарную добычу: {state.LegendaryTokens} (выдавай только как награду за выпавшее в колесе дороги испытание или финал арки).",
                $"- Legendary loot permissions: {state.LegendaryTokens} (hand out only as a reward for a trial rolled on the road wheel or an arc finale).")
            : L("- Разрешений на легендарную добычу: 0 — легендарных вещей сейчас выдавать нельзя.", "- Legendary loot permissions: 0 — no legendary items may be handed out now."));

        var npcs = state.ImportantCharacters.Where(n => n.Category != "nearby" && !string.IsNullOrWhiteSpace(n.Name))
            .Select(n => Cut(n.Name, 40) +
                         (string.IsNullOrWhiteSpace(n.Role) ? "" : $", {Cut(n.Role, 30)}") +
                         (string.IsNullOrWhiteSpace(n.Attitude) ? "" : $" ({Cut(n.Attitude, 30)})") +
                         (n.Shop is { } shop ? L($" [лавка: {ItemEconomy.Merchant(shop.Kind).Title.ToLowerInvariant()}, товаров {shop.Items.Count}, у торговца {shop.Gold} з.]",
                                                 $" [shop: {ItemEconomy.Merchant(shop.Kind).Title.ToLowerInvariant()}, {shop.Items.Count} goods, the merchant has {shop.Gold} {Genre.Coin}]") : ""))
            .ToList();
        sb.AppendLine(npcs.Count > 0 ? L("- Важные персонажи: ", "- Important characters: ") + string.Join("; ", npcs) + "." : L("- Важные персонажи: пока нет.", "- Important characters: none yet."));

        // Неподалёку показываем только то, что рядом с текущей сценой: торговцы города не должны
        // светиться, пока герой гуляет по лесу. Оставшиеся в других местах — отдельной строкой,
        // чтобы мастер знал, что эти люди никуда не делись, и не заводил дубликатов.
        if (!string.IsNullOrWhiteSpace(state.ScenePlace))
        {
            sb.AppendLine(L("- Место сцены: ", "- Scene place: ") + Cut(state.ScenePlace, 60) + ".");
        }

        var here = state.NearbyHere().ToList();
        sb.AppendLine(here.Count > 0
            ? L("- Взаимодействия неподалёку: ", "- Nearby interactions: ") + string.Join("; ", here.Select(n =>
                Cut(n.Name, 40) + (string.IsNullOrWhiteSpace(n.Role) ? "" : $", {Cut(n.Role, 30)}") +
                (n.Shop is null ? "" : L(" [торговец]", " [merchant]")))) + "."
            : L("- Взаимодействия неподалёку: нет.", "- Nearby interactions: none."));

        var away = state.ImportantCharacters
            .Where(n => n.Category == "nearby" && !here.Contains(n) && !string.IsNullOrWhiteSpace(n.Name))
            .Select(n => Cut(n.Name, 40) + (string.IsNullOrWhiteSpace(n.NearbyPlace) ? "" : $" ({Cut(n.NearbyPlace, 30)})"))
            .ToList();
        if (away.Count > 0)
        {
            sb.AppendLine(L($"- Остались в других местах: {string.Join("; ", away)} — по возвращении снова станут рядом.", $"- Left in other places: {string.Join("; ", away)} — they will be nearby again on return."));
        }

        if (state.PendingStreetOffer is { } street)
        {
            sb.AppendLine(L($"- Уличное предложение (разовое, открыто): «{Cut(street.Item.Name, 40)}» ×{Math.Max(1, street.Item.Quantity)} за {street.Price} з.",
                            $"- Street offer (one-off, open): \"{Cut(street.Item.Name, 40)}\" ×{Math.Max(1, street.Item.Quantity)} for {street.Price} {Genre.Coin}.") +
                          $"{(string.IsNullOrWhiteSpace(street.Seller) ? "" : $" — {Cut(street.Seller, 30)}")}" +
                          L(" Покупка: street_deal action=buy; отказ: street_deal action=reject.", " Buy: street_deal action=buy; refuse: street_deal action=reject."));
        }

        var notables = state.Notables.Where(n => !string.IsNullOrWhiteSpace(n.Name))
            .Select(n => $"{Cut(n.Name, 40)} — {Cut(n.Note, 100)}").ToList();
        if (notables.Count > 0) sb.AppendLine(L("- Не забыть: ", "- Don't forget: ") + string.Join("; ", notables) + ".");

        var party = state.Party.Select(p =>
        {
            var pl = Progression.ParseLevel(p.Level);
            var atk = ItemStats.BestAttack(p);
            var death = p.HpCurrent <= 0
                ? L($"; при смерти {(p.DeathState.Length > 0 ? p.DeathState : "dying")}, успехи {p.DeathSaveSuccesses}/3, провалы {p.DeathSaveFailures}/3",
                    $"; dying {(p.DeathState.Length > 0 ? p.DeathState : "dying")}, successes {p.DeathSaveSuccesses}/3, failures {p.DeathSaveFailures}/3")
                : "";
            var skillsText = string.Join(", ", p.LearnedSkills.Select(s => $"{s.Name} {L("ст.", "t.")}{s.Rank} ({s.ManaCost} {L("м.", "m.")}) — {Cut(Progression.ExplainPowerDice(s.Description, pl), 70)}"));
            return L($"{Cut(p.Name, 40)} ({Cut(p.Race, 20)} {Cut(p.CharClass, 25)}, роль {Progression.ArchetypeOf(p).Title.ToLowerInvariant()}, ур.{pl}, {Cut(p.Gender, 15)}, " +
                   $"ХП {p.HpCurrent}/{p.HpMax}, мана {p.ManaCurrent}/{p.ManaMax}, щит {p.Shield}, КБ {ItemStats.ArmorClass(p)}, {atk.Title}: атака {atk.Attack:+#;-#;+0} урон {atk.DamageText}; " +
                   $"навыки: {skillsText}{death})",
                   $"{Cut(p.Name, 40)} ({Cut(p.Race, 20)} {Cut(p.CharClass, 25)}, role {Progression.ArchetypeOf(p).Title.ToLowerInvariant()}, lvl {pl}, {Cut(p.Gender, 15)}, " +
                   $"HP {p.HpCurrent}/{p.HpMax}, mana {p.ManaCurrent}/{p.ManaMax}, shield {p.Shield}, AC {ItemStats.ArmorClass(p)}, {atk.Title}: attack {atk.Attack:+#;-#;+0} damage {atk.DamageText}; " +
                   $"skills: {skillsText}{death})");
        }).ToList();
        sb.AppendLine(party.Count > 0 ? L($"- Группа ({party.Count}/3): {string.Join("; ", party)}. Компаньоны действуют самостоятельно.", $"- Party ({party.Count}/3): {string.Join("; ", party)}. Companions act on their own.") : L("- Группа: герой без компаньонов.", "- Party: the hero has no companions."));

        var candidates = state.CompanionCandidates
            .Where(c => !string.IsNullOrWhiteSpace(c.Name) && c.Stage != "recruited")
            .Select(c => L($"{Cut(c.Name, 40)} [{c.Stage}, {c.Source}; совместных испытаний {c.SharedTrials}] — {Cut(c.Concept, 90)}; " +
                         $"мотив: {Cut(c.Motivation, 80)}; личная цель: {Cut(c.PersonalGoal, 80)}; условие: {Cut(c.JoinCondition, 100)}",
                         $"{Cut(c.Name, 40)} [{c.Stage}, {c.Source}; shared trials {c.SharedTrials}] — {Cut(c.Concept, 90)}; " +
                         $"motive: {Cut(c.Motivation, 80)}; personal goal: {Cut(c.PersonalGoal, 80)}; condition: {Cut(c.JoinCondition, 100)}") +
                         (c.RoleOffered ? L("; выбор роли уже показан", "; the role choice has already been shown") : "") +
                         (c.Milestones.Count > 0 ? L("; последний итог: ", "; last outcome: ") + Cut(c.Milestones[^1], 100) : ""))
            .ToList();
        if (candidates.Count > 0) sb.AppendLine(L("- Кандидаты в спутники: ", "- Companion candidates: ") + string.Join("; ", candidates) + ".");

        if (state.Party.Count < 3)
        {
            var openCandidates = state.CompanionCandidates.Where(c => c.Stage is "introduced" or "proven" or "ready").ToList();
            if (openCandidates.Count == 0 && state.StoryArcs.Any(a => a.State == "active"))
                sb.AppendLine(L("- Воронка спутников: группа неполна и открытых кандидатов нет. В ближайшие 1–2 содержательные сцены органично введи одного NPC, связанного с аркой, миром или гильдией: сначала update_npc, затем update_companion_candidate action=introduce. Одиночная игра допустима — не навязывай вступление.",
                                "- Companion funnel: the party is not full and there are no open candidates. In the next 1–2 meaningful scenes naturally introduce one NPC connected to the arc, the world or the guild: first update_npc, then update_companion_candidate action=introduce. Solo play is fine — do not force anyone to join."));
            else if (openCandidates.Any(c => c.Stage == "introduced"))
                sb.AppendLine(L("- Воронка спутников: у кандидата на этапе introduced должна случиться значимая совместная сцена; после её фактического итога вызови update_companion_candidate action=shared_trial.",
                                "- Companion funnel: a candidate at the introduced stage needs a meaningful shared scene; after its actual outcome call update_companion_candidate action=shared_trial."));
            else if (openCandidates.Any(c => c.Stage == "proven"))
                sb.AppendLine(L("- Воронка спутников: после выполнения условия проведи явный разговор о совместном пути; только при взаимном согласии вызови update_companion_candidate action=ready.",
                                "- Companion funnel: once the condition is met, hold an explicit conversation about travelling together; only with mutual consent call update_companion_candidate action=ready."));
            else if (openCandidates.Any(c => c.Stage == "ready" && !c.RoleOffered))
                sb.AppendLine(L("- Воронка спутников: согласие уже получено — вызови ask_companion_role. Не выбирай боевую роль за игрока.",
                                "- Companion funnel: consent has been obtained — call ask_companion_role. Do not pick the combat role for the player."));
            else if (openCandidates.Any(c => c.Stage == "ready" && c.RoleOffered))
                sb.AppendLine(L("- Воронка спутников: карточки ролей уже показаны; дождись сообщения игрока с ключом роли и вызови recruit_companion.",
                                "- Companion funnel: the role cards have been shown; wait for the player's message with the role key and call recruit_companion."));
        }

        var advs = state.Adversaries.Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .Select(a =>
            {
                var cr = string.IsNullOrWhiteSpace(a.Level) ? "" : L(", ур. ", ", lvl ") + Cut(a.Level, 12);
                var stats = a.Ac > 0 ? L($", КБ {a.Ac}, атака {a.AttackBonus:+#;-#;+0}{(a.Attacks > 1 ? $"×{a.Attacks}" : "")} урон {a.Damage}, DC {a.SaveDc}",
                                         $", AC {a.Ac}, attack {a.AttackBonus:+#;-#;+0}{(a.Attacks > 1 ? $"×{a.Attacks}" : "")} damage {a.Damage}, DC {a.SaveDc}") : "";
                var stag = a.StaggerMax > 0 ? L($", стойкость {a.Stagger}/{a.StaggerMax}", $", poise {a.Stagger}/{a.StaggerMax}") : "";
                var leg = a.Legendary > 0 ? L($", легенд. действий {a.Legendary}", $", legendary actions {a.Legendary}") : "";
                var outText = a.Morale == "fled" ? L(" — БЕЖАЛ, вне боя", " — FLED, out of the fight") : a.Morale == "surrendered" ? L(" — СДАЛСЯ, вне боя", " — SURRENDERED, out of the fight") : a.HpCurrent <= 0 ? L(" — повержен", " — defeated") : "";
                return L($"{Cut(a.Name, 40)} [{a.ThreatTier}]{outText} (ХП {a.HpCurrent}/{a.HpMax}, щит {a.Shield}{cr}{stats}{stag}{leg}; эффекты {string.Join(", ",a.Effects)})",
                         $"{Cut(a.Name, 40)} [{a.ThreatTier}]{outText} (HP {a.HpCurrent}/{a.HpMax}, shield {a.Shield}{cr}{stats}{stag}{leg}; effects {string.Join(", ",a.Effects)})");
            })
            .ToList();
        sb.AppendLine(advs.Count > 0 ? L("- Противники в бою: ", "- Adversaries in combat: ") + string.Join("; ", advs) + "." : L("- Противники: нет.", "- Adversaries: none."));

        // Очередь хода — те же числа, что видит игрок в окне боя: без неё модель забывает порядок
        // между ходами и пропускает часть участников. Строгий порядок нужен только пока бой идёт:
        // после победы эта строка продолжала бы толкать модель к новым раундам против мертвецов,
        // и вместо итогов с выбором ход уходил в бой.
        var fighting = state.InCombat();
        if (fighting && state.Initiative.Count > 0)
        {
            sb.AppendLine(L($"- Очередь хода (инициатива): {Initiative.Line(state.Initiative)}. Веди раунд строго по ней — " +
                          "номер участника не меняется от порядка твоих действий, каждый должен сходить за раунд.",
                          $"- Turn order (initiative): {Initiative.Line(state.Initiative)}. Run the round strictly by it — " +
                          "a participant's number does not change with the order of your actions, everyone must act each round.") +
                          MissingNote(state));
            sb.AppendLine(state.CombatRound > 0 && state.CombatCurrentActor.Length > 0
                ? L($"- Текущий ход: раунд {state.CombatRound}, действует {state.CombatCurrentActor}. После полного действия вызови combat_turn action=advance с actor.",
                    $"- Current turn: round {state.CombatRound}, {state.CombatCurrentActor} acts. After the full action call combat_turn action=advance with actor.")
                : L("- Счётчик раунда не запущен: после полной инициативы вызови combat_turn action=start.", "- The round counter is not running: after full initiative call combat_turn action=start."));
        }
        else if (fighting)
        {
            sb.AppendLine(L("- Очередь хода: инициатива не брошена. Вызови initiative roll_all=true — харнес бросит всем и начнёт раунд 1.", "- Turn order: initiative has not been rolled. Call initiative roll_all=true — the harness rolls for everyone and starts round 1."));
        }

        if (fighting)
        {
            sb.AppendLine(L("- Действия в бою разыгрывай resolve_attack (атака, навык, лечение, щит, эффект) — он сам бросает, считает КБ, урон, эффекты и мораль. " +
                          "Длительности эффектов и урон во времени тикает combat_turn.",
                          "- Resolve combat actions with resolve_attack (an attack, a skill, healing, a shield, an effect) — it rolls itself and calculates AC, damage, effects and morale. " +
                          "combat_turn ticks effect durations and damage over time."));
        }

        var cities = state.Cities.Where(x => !string.IsNullOrWhiteSpace(x.Name)).Select(x => Cut(x.Name, 40)).ToList();
        sb.AppendLine(cities.Count > 0 ? L("- Места: ", "- Places: ") + string.Join("; ", cities) + "." : L("- Места: не описаны.", "- Places: not described."));

        var quests = state.Quests.Where(q => !string.IsNullOrWhiteSpace(q.Name))
            .Select(q => $"{Cut(q.Name, 40)} [{q.State switch { "done" => L("выполнено", "done"), "failed" => L("провалено", "failed"), _ => L("принято", "accepted") }}]")
            .ToList();
        sb.AppendLine(quests.Count > 0 ? L("- Задания: ", "- Quests: ") + string.Join("; ", quests) + "." : L("- Задания: нет.", "- Quests: none."));

        var arc = state.StoryArcs.FirstOrDefault(a => a.State == "active");
        if (arc is not null)
        {
            sb.AppendLine(L("- Активная арка: ", "- Active arc: ") + $"{Cut(arc.Name, 60)} — {Cut(arc.Premise, 200)}" +
                          (string.IsNullOrWhiteSpace(arc.Antagonist) ? "" : L(" Противник: ", " Adversary: ") + Cut(arc.Antagonist, 80) + ".") +
                          (string.IsNullOrWhiteSpace(arc.NextLead) ? "" : L(" Следующий ориентир: ", " Next lead: ") + Cut(arc.NextLead, 120) + ".") +
                          (arc.Beats.Count > 0 ? L(" Старый внутренний план: ", " Legacy internal plan: ") + string.Join(" → ", arc.Beats.Select(b => Cut(b, 60))) + "." : "") +
                          (arc.Events.Count > 0 ? L(" Уже произошло: ", " Already happened: ") + string.Join(" → ", arc.Events.Select(e => Cut(e, 60))) + "." : ""));
        }
        else
        {
            sb.AppendLine(L("- Активная арка: нет.", "- Active arc: none."));
        }

        var pendingRumors = state.RumorClues.Where(r => !r.Recalled).Select(r => $"{Cut(r.Source, 35)} → {Cut(r.LinkedTo, 45)}").ToList();
        if (pendingRumors.Count > 0)
        {
            sb.AppendLine(L($"- Купленные подсказки (не раскрывай прямо): {string.Join("; ", pendingRumors)}. Когда подсказка уместна, предложи действие «Попробовать вспомнить…»; точное содержание получай через recall_rumor только после выбора игрока.",
                            $"- Purchased hints (do not reveal directly): {string.Join("; ", pendingRumors)}. When a hint fits, offer the action \"Try to remember…\"; get the exact content via recall_rumor only after the player's choice."));
        }

        sb.AppendLine(string.IsNullOrWhiteSpace(state.WorldState)
            ? L("- Сейчас: место и время не зафиксированы (set_world_state).", "- Now: place and time are not recorded (set_world_state).")
            : L("- Сейчас: ", "- Now: ") + Cut(state.WorldState, 300));
        AppendWorldClock(sb, state);

        var evs = state.WorldEvents.Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .Select(e => $"{Cut(e.Name, 40)} [{(e.State == "ongoing" ? L("идёт", "ongoing") : e.State == "upcoming" ? L("скоро", "upcoming") : L("кончилось", "ended"))}]")
            .ToList();
        if (evs.Count > 0)
        {
            sb.AppendLine(L("- События мира: ", "- World events: ") + string.Join("; ", evs) + ".");
        }

        if (GetPhase(chatId) == CampaignPhase.SessionZero)
        {
            sb.AppendLine();
            sb.AppendLine(L("Шаблоны для заполнения (create_file поверх шаблона, заголовки разделов сохрани):", "Templates to fill in (create_file over the template, keep the section headings):"));
            sb.AppendLine($"- World.md: {CampaignTemplates.Outline(CampaignTemplates.WorldTemplate)}.");
            sb.AppendLine(L("- Factions.md: раздел на фракцию — Кто они · Лидер · Цели · Ресурсы · Отношение к герою · Союзники/враги.", "- Factions.md: a section per faction — Who they are · Leader · Goals · Resources · Attitude to the hero · Allies/enemies."));
            sb.AppendLine($"- Secrets.md: {CampaignTemplates.Outline(CampaignTemplates.SecretsTemplate)}.");
        }

        sb.AppendLine();
        sb.AppendLine(L("Файлы кампании (read_file): World.md, Rules.md, Factions.md, Bestiary.md, Journal.md, Secrets.md, Hero.md, Inventory.md, Quests.md, Story_Arc.md, Instances.md, Cities.md, Worldstate.md, Adversaries.md, Characters/<имя>.md (с лавкой, если NPC торгует).",
                        "Campaign files (read_file): World.md, Rules.md, Factions.md, Bestiary.md, Journal.md, Secrets.md, Hero.md, Inventory.md, Quests.md, Story_Arc.md, Instances.md, Cities.md, Worldstate.md, Adversaries.md, Characters/<name>.md (with a shop if the NPC trades)."));

        if (!string.IsNullOrWhiteSpace(journalTail))
        {
            sb.AppendLine();
            sb.AppendLine(L("Ранние сообщения чата скрыты из контекста. Последние записи Journal.md:", "Early chat messages are hidden from the context. The latest Journal.md entries:"));
            sb.AppendLine(journalTail);
        }

        // В киберпанке сводка говорит эдди, ОЗУ и фиксерами — как промпт и результаты инструментов.
        return Genre.Localize(sb.ToString());
    }

    private (bool Ok, string Result) UpdateCharacter(string? chatId, JsonObject args)
    {
        if (chatId is null)
        {
            return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        }

        var state = _rpg.GetOrCreate(chatId);
        var c = state.Character;
        var changes = new List<string>();

        if (Arg(args, "name") is { } name && name.Trim().Length > 0)
        {
            c.Name = name.Trim();
            changes.Add(L("имя → ", "name → ") + c.Name);
        }

        if (OptInt(args, "level", int.MinValue) is var lvl && lvl != int.MinValue)
        {
            c.Level = Math.Max(1, lvl).ToString();
            changes.Add(L("уровень → ", "level → ") + c.Level);
        }

        if (OptInt(args, "hp_current", int.MinValue) is var hpCur && hpCur != int.MinValue)
        {
            c.HpCurrent = Math.Max(0, hpCur);
            if (c.HpCurrent > 0) CombatRules.Revive(c); else CombatRules.EnterDying(c);
            changes.Add($"HP → {c.HpCurrent}/{c.HpMax}");
        }
        if (OptInt(args, "hp_delta", 0) is var hpDelta && hpDelta != 0)
        {
            if (hpDelta < 0 && c.Shield > 0) { var absorbed = Math.Min(c.Shield, -hpDelta); c.Shield -= absorbed; hpDelta += absorbed; }
            if (hpDelta < 0) CombatRules.DamageWhileDying(c);
            c.HpCurrent = Math.Clamp(c.HpCurrent + hpDelta, 0, Math.Max(c.HpMax, c.HpCurrent));
            if (c.HpCurrent > 0) CombatRules.Revive(c); else CombatRules.EnterDying(c);
            changes.Add($"HP → {c.HpCurrent}/{c.HpMax}, {L("щит", "shield")} {c.Shield}");
        }

        if (OptInt(args, "hp_max", 0) is var hpMax && hpMax > 0)
        {
            c.HpMax = hpMax;
            if (c.HpCurrent > hpMax)
            {
                c.HpCurrent = hpMax;
            }

            changes.Add(L("макс. HP → ", "max HP → ") + hpMax);
        }

        if (OptInt(args, "mana_current", int.MinValue) is var manaCur && manaCur != int.MinValue)
        {
            c.ManaCurrent = Math.Max(0, manaCur);
            changes.Add(L("мана → ", "mana → ") + $"{c.ManaCurrent}/{c.ManaMax}");
        }

        if (OptInt(args, "mana_max", 0) is var manaMax && manaMax > 0)
        {
            c.ManaMax = manaMax;
            if (c.ManaCurrent > manaMax)
            {
                c.ManaCurrent = manaMax;
            }

            changes.Add(L("макс. мана → ", "max mana → ") + manaMax);
        }
        if (OptInt(args, "shield", int.MinValue) is var shield && shield != int.MinValue) { c.Shield = Math.Max(0, shield); changes.Add(L("щит → ", "shield → ") + c.Shield); }

        if (OptInt(args, "gold", int.MinValue) is var gold && gold != int.MinValue)
        {
            c.Gold = Math.Max(0, gold);
            changes.Add(L("золото → ", "gold → ") + c.Gold);
        }

        if (OptInt(args, "xp", int.MinValue) is var xp && xp != int.MinValue)
        {
            c.Xp = Math.Max(0, xp);
            changes.Add(L("опыт → ", "XP → ") + c.Xp);
        }

        if (Str(args, "add_status") is { } add && add.Trim().Length > 0)
        {
            var applied = CombatEffects.AddOrRefresh(c.Status, add, Num(args, "rounds"), state.InCombat());
            changes.Add(L("состояние +", "condition +") + applied);
        }

        if (Str(args, "remove_status") is { } rem && rem.Trim().Length > 0)
        {
            var removed = CombatEffects.Remove(c.Status, rem);
            changes.Add(removed > 0 ? L("состояние −", "condition −") + rem.Trim() : L($"состояние «{rem.Trim()}» не найдено (есть: {(c.Status.Count > 0 ? string.Join(", ", c.Status) : "нет")})", $"condition \"{rem.Trim()}\" not found (present: {(c.Status.Count > 0 ? string.Join(", ", c.Status) : "none")})"));
        }

        if (Arg(args, "notes") is { } notes && notes.Trim().Length > 0)
        {
            c.Notes = string.IsNullOrWhiteSpace(c.Notes) ? notes.Trim() : c.Notes!.TrimEnd() + "\n" + notes.Trim();
            changes.Add(L("заметки дополнены", "notes extended"));
        }

        if (changes.Count == 0)
        {
            return (false, L("Ошибка: не передано ни одного изменяемого поля.", "Error: no field to change was passed."));
        }

        _rpg.Save(chatId, state);
        return (true, "OK: " + string.Join(", ", changes) + ".");
    }

    private (bool Ok, string Result) SetStat(string? chatId, JsonObject args)
    {
        if (chatId is null)
        {
            return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        }

        var statName = (Arg(args, "stat") ?? "").Trim().ToLowerInvariant();
        if (!Enum.TryParse<DndStat>(statName, ignoreCase: true, out var stat))
        {
            return (false, L("Ошибка: stat должен быть одним из: str, dex, con, int, wis, cha.", "Error: stat must be one of: str, dex, con, int, wis, cha."));
        }

        var value = OptInt(args, "value", 0);
        if (value is < 1 or > 30)
        {
            return (false, L("Ошибка: value должен быть в диапазоне 1–30.", "Error: value must be in the range 1–30."));
        }

        var state = _rpg.GetOrCreate(chatId);
        state.Character.SetStat(stat, value);
        _rpg.Save(chatId, state);
        return (true, $"OK: {DndStatNames.Full[(int)stat]} → {value} ({L("мод", "mod")} {DndStatNames.ModText(value)}).");
    }

    /// <summary>
    /// Старый списочный update_inventory (его уже нет в схемах, но модель могла запомнить его по истории):
    /// переводим вызов на сумку-сетку, которую видит игрок.
    /// </summary>
    private (bool Ok, string Result) UpdateInventoryLegacy(string? chatId, JsonObject args)
    {
        var grid = new JsonObject { ["name"] = Arg(args, "name") ?? "" };
        if (args["note"] is { } note)
        {
            grid["note"] = note.DeepClone();
        }

        var qty = OptInt(args, "quantity", int.MinValue);
        if (qty == 0)
        {
            grid["remove"] = true;
        }
        else if (qty != int.MinValue)
        {
            grid["quantity"] = qty;
        }

        return UpdateGridItem(chatId, grid);
    }

    private (bool Ok, string Result) UpdateNpc(string? chatId, JsonObject args)
    {
        if (chatId is null)
        {
            return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        }

        var name = (Arg(args, "name") ?? "").Trim();
        if (name.Length == 0)
        {
            return (false, L("Ошибка: не указано имя NPC (name).", "Error: the NPC name (name) is not given."));
        }

        var state = _rpg.GetOrCreate(chatId);
        var npc = state.ImportantCharacters.Concat(state.Notables).FirstOrDefault(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var created = npc is null;

        var requestedCategory = (Arg(args, "category") ?? "").Trim().ToLowerInvariant();
        if (requestedCategory is not ("important" or "nearby" or "notable"))
        {
            requestedCategory = "";
        }

        if (OptBool(args, "remove"))
        {
            if (npc is not null)
            {
                state.ImportantCharacters.Remove(npc);
                state.Notables.Remove(npc);
                _rpg.Save(chatId, state);
            }
            return (true, L($"OK: персонаж «{name}» убран из текущих списков.", $"OK: the character \"{name}\" is removed from the current lists."));
        }

        if (npc is null)
        {
            npc = new ImportantNpc { Name = name, Category = requestedCategory.Length > 0 ? requestedCategory : "important" };
            (npc.Category == "notable" ? state.Notables : state.ImportantCharacters).Add(npc);
        }
        else if (requestedCategory.Length > 0)
        {
            state.ImportantCharacters.Remove(npc);
            state.Notables.Remove(npc);
            npc.Category = requestedCategory;
            (npc.Category == "notable" ? state.Notables : state.ImportantCharacters).Add(npc);
        }

        // Nearby означает «доступен в текущей сцене» — привязываем к ней: когда герой уйдёт,
        // такой персонаж исчезнет из «неподалёку», а по возвращении снова появится.
        if (npc.Category == "nearby" && (created || requestedCategory == "nearby"))
        {
            npc.NearbyPlace = state.ScenePlace ?? "";
        }

        if (Arg(args, "note") is { } note && note.Trim().Length > 0)
        {
            npc.Note = note.Trim();
        }

        if (Arg(args, "attitude") is { } att && att.Trim().Length > 0)
        {
            npc.Attitude = att.Trim();
        }

        if (Arg(args, "level") is { } lvl && lvl.Trim().Length > 0)
        {
            npc.Level = lvl.Trim();
        }

        if (Arg(args, "abilities") is { } ab && ab.Trim().Length > 0)
        {
            npc.Abilities = ab.Trim();
        }

        if (Arg(args, "role") is { } role && role.Trim().Length > 0)
        {
            npc.Role = role.Trim();
        }

        if (Arg(args, "location") is { } loc && loc.Trim().Length > 0)
        {
            npc.Location = loc.Trim();
        }

        if (OptInt(args, "hp_current", int.MinValue) is var hpCur && hpCur != int.MinValue)
        {
            npc.HpCurrent = Math.Max(0, hpCur);
        }

        if (OptInt(args, "hp_max", 0) is var hpMax && hpMax > 0)
        {
            npc.HpMax = hpMax;
            if (npc.HpCurrent > hpMax)
            {
                npc.HpCurrent = hpMax;
            }
        }

        _rpg.Save(chatId, state);
        return (true, L($"OK: {(created ? "создан" : "обновлён")} персонаж «{npc.Name}» [{npc.Category}].", $"OK: {(created ? "created" : "updated")} the character \"{npc.Name}\" [{npc.Category}]."));
    }

    // ===== Уличная сделка (разовое событие) =====

    /// <summary>
    /// Разовая уличная сделка: харнес подбирает вещь (только редкую или дешёвую) и цену,
    /// а продающий остаётся событием разговора — в списки персонажей не пишется.
    /// </summary>
    private (bool Ok, string Result) MakeStreetOffer(string? chatId, JsonObject args)
    {
        if (chatId is null)
        {
            return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        }

        var state = _rpg.GetOrCreate(chatId);
        var rng = Random.Shared;
        var seller = (Arg(args, "seller") ?? "").Trim();

        // У прохожих не бывает артефактов, диагностик и сюжетных вещей; редкости — по мерке улицы,
        // а не по мерке лавок (цена всё равно честная, по правилам игры).
        var defs = _catalog.All
            .Where(d => !d.Art && !d.Diag && !ItemEconomy.StoryOnly.Contains(d.Id) && _economy.BasePrice(d.Id) > 0)
            .ToList();

        var rarePool = defs
            .Where(d => ItemEconomy.Category(_economy.CategoryOf(d.Id)) is { Gear: true }
                        && _economy.BasePrice(d.Id) is >= 20 and <= 200)
            .ToList();
        var cheapPool = defs.Where(d => _economy.BasePrice(d.Id) <= 10).ToList();

        var wantRare = rng.Next(2) == 0;
        var pool = wantRare ? rarePool : cheapPool;
        if (pool.Count == 0)
        {
            pool = wantRare ? cheapPool : rarePool;
        }

        if (pool.Count == 0)
        {
            return (false, L("Ошибка: не удалось подобрать вещь для уличной сделки — каталог предметов пуст.", "Error: could not pick an item for a street deal — the item catalog is empty."));
        }

        var useRare = pool == rarePool;
        var def = pool[rng.Next(pool.Count)];
        var rarity = useRare ? "rare" : "common";
        var quantity = useRare ? 1 : rng.Next(1, 4);

        var item = new GridItem
        {
            Name = def.Name,
            Icon = def.Id,
            W = def.W,
            H = def.H,
            Slot = def.EquipSlot,
            TwoHanded = def.TwoHanded,
            Rarity = rarity,
            Quantity = quantity,
            Note = ItemEconomy.RarityNote(rarity),
        };
        ItemStats.ApplyBase(item);
        ItemStats.RollBonuses(item, rng);
        if (rarity != "common")
        {
            item.Name = $"{def.Name} ({ItemEconomy.RarityTitle(rarity)})";
        }

        // Дешёвая мелочь — просто честная стоимость стопки; редкая — небольшая вилка вокруг неё.
        var unit = _economy.UnitValue(item);
        var price = useRare
            ? Math.Max(1, (int)Math.Ceiling(unit * (0.9 + rng.NextDouble() * 0.3)))
            : Math.Max(1, unit * quantity);
        item.Value = unit;

        state.PendingStreetOffer = new StreetOffer
        {
            Seller = seller,
            Item = item,
            Price = price,
            Kind = useRare ? "rare" : "cheap",
        };
        _rpg.Save(chatId, state);

        var who = seller.Length > 0 ? L($"Прохожий «{seller}»", $"The passer-by \"{seller}\"") : L("Прохожий", "A passer-by");
        var what = useRare ? L("редкость: ", "rarity: ") + ItemEconomy.RarityTitle(rarity) : L("дешёвая мелочь", "a cheap trinket");
        return (true, L(
            $"OK: уличное предложение готово — {who} продаёт «{item.Name}» ×{quantity} за {price} з. ({what}; {ItemStats.Describe(item)}). " +
            "Предмет и цену подобрал харнес, не меняй их. Опиши предложение и закрой реплику через offer_choices: " +
            "после согласия игрока вызови street_deal action=buy, после отказа — street_deal action=reject. " +
            "Продающий остаётся разовым событием: не создавай для него NPC и не добавляй в списки персонажей.",
            $"OK: the street offer is ready — {who} sells \"{item.Name}\" ×{quantity} for {price} {Genre.Coin} ({what}; {ItemStats.Describe(item)}). " +
            "The harness picked the item and the price, do not change them. Describe the offer and close the line with offer_choices: " +
            "after the player agrees call street_deal action=buy, after a refusal — street_deal action=reject. " +
            "The seller remains a one-off event: do not create an NPC for them and do not add them to the character lists."));
    }

    /// <summary>Купить предложенное у прохожим или отказаться: списывает/сохраняет золото честно, посчитанным харнессом.</summary>
    private (bool Ok, string Result) CloseStreetDeal(string? chatId, JsonObject args)
    {
        if (chatId is null)
        {
            return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        }

        var state = _rpg.GetOrCreate(chatId);
        var offer = state.PendingStreetOffer;
        if (offer is null)
        {
            return (false, L("Ошибка: открытого уличного предложения нет — оно уже куплено, отклонено или не создавалось.", "Error: there is no open street offer — it was already bought, rejected or never created."));
        }

        var action = (Arg(args, "action") ?? "buy").Trim().ToLowerInvariant();
        if (action is not ("buy" or "reject"))
        {
            return (false, L("Ошибка: action должен быть buy (купить) или reject (отказаться).", "Error: action must be buy or reject."));
        }

        if (action == "reject")
        {
            state.PendingStreetOffer = null;
            _rpg.Save(chatId, state);
            return (true, L("OK: предложение отклонено — золото не списано, сделка закрыта. Ничего не создано; заверши ответ через offer_choices.", "OK: the offer is rejected — no gold was deducted, the deal is closed. Nothing was created; finish the reply with offer_choices."));
        }

        var quantity = Math.Max(1, offer.Item.Quantity);
        if (state.Character.Gold < offer.Price)
        {
            return (false, L($"Не хватает золота: «{offer.Item.Name}» стоит {offer.Price} з., у героя {state.Character.Gold} з. Предложение остаётся открытым.", $"Not enough gold: \"{offer.Item.Name}\" costs {offer.Price} {Genre.Coin}, the hero has {state.Character.Gold} {Genre.Coin}. The offer stays open."));
        }

        var taken = InventoryOps.CloneForTransfer(offer.Item, quantity);
        taken.Value = _economy.UnitValue(offer.Item);
        var spot = InventoryOps.FindFree(state.Grid, taken.W, taken.H, null, null, null, RpgState.GridCols, RpgState.GridRows);
        if (spot is null)
        {
            return (false, L($"В сумке нет места {taken.W}×{taken.H} для «{offer.Item.Name}». Предложение остаётся открытым.", $"No {taken.W}×{taken.H} room in the bag for \"{offer.Item.Name}\". The offer stays open."));
        }

        (taken.Col, taken.Row) = spot.Value;
        state.Grid.Add(taken);
        state.Character.Gold -= offer.Price;
        state.PendingStreetOffer = null;
        _rpg.Save(chatId, state);

        return (true, L(
            $"OK: куплено у прохожего: «{taken.Name}» ×{quantity} за {offer.Price} з. Остаток золота: {state.Character.Gold} з. " +
            "Вещь в сумке, сделка закрыта. Опиши, как прошла покупка, и заверши ответ через offer_choices.",
            $"OK: bought from the passer-by: \"{taken.Name}\" ×{quantity} for {offer.Price} {Genre.Coin}. Gold left: {state.Character.Gold} {Genre.Coin}. " +
            "The item is in the bag, the deal is closed. Describe how the purchase went and finish the reply with offer_choices."));
    }

    // ===== Торговля =====

    private static double? OptDouble(JsonObject args, string key)
    {
        try
        {
            return args[key] switch
            {
                JsonValue v when v.TryGetValue<double>(out var d) => d,
                JsonValue v when v.TryGetValue<string>(out var str) &&
                                 double.TryParse(str.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                                     System.Globalization.CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    private static ImportantNpc? FindNpc(RpgState state, string name) =>
        state.ImportantCharacters.Concat(state.Notables).FirstOrDefault(n => n.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string? ParseTier(string? raw)
    {
        var t = (raw ?? "").Trim().ToLowerInvariant();
        if (t.Length == 0)
        {
            return null;
        }

        if (t is "village" || t.Contains("дерев") || t.Contains("село") || t.Contains("хутор") || t.Contains("hamlet") || t.Contains("small"))
        {
            return "village";
        }

        if (t is "city" or "capital" || t.Contains("столиц") || t.Contains("большой") || t.Contains("metropol") || t.Contains("large"))
        {
            return "city";
        }

        return "town";
    }

    private static MerchantKind? ParseKind(string? raw)
    {
        var k = (raw ?? "").Trim();
        if (k.Length == 0)
        {
            return null;
        }

        return ItemEconomy.Merchants.FirstOrDefault(m => m.Key.Equals(k, StringComparison.OrdinalIgnoreCase))
               ?? ItemEconomy.Merchants.FirstOrDefault(m => m.Title.Contains(k, StringComparison.OrdinalIgnoreCase)
                                                        || k.Contains(m.Title, StringComparison.OrdinalIgnoreCase));
    }

    private static (List<string> Ok, List<string> Bad) ParseCategories(string? raw)
    {
        var ok = new List<string>();
        var bad = new List<string>();
        foreach (var part in (raw ?? "").Split(',', ';'))
        {
            var key = part.Trim().ToLowerInvariant();
            if (key.Length == 0)
            {
                continue;
            }

            if (ItemEconomy.Category(key) is not null)
            {
                ok.Add(key);
            }
            else
            {
                bad.Add(key);
            }
        }

        return (ok, bad);
    }

    private string ShopSummary(MerchantShop shop, CharacterSheet hero, int max = 30)
    {
        var wares = shop.Items
            .Select(i => $"{i.Name}{(i.Quantity > 1 ? $" x{i.Quantity}" : "")} — {_economy.BuyPrice(shop, i, hero)} {L("з.", Genre.Coin)}" +
                         (ItemEconomy.RarityKey(i.Rarity) == "common" ? "" : $" [{ItemEconomy.RarityTitle(i.Rarity)}]"))
            .Take(max)
            .ToList();
        return wares.Count == 0
            ? L("лавка пуста", "the shop is empty")
            : string.Join("; ", wares) + (shop.Items.Count > max ? L(L($"; …ещё {shop.Items.Count - max}", $"; …{shop.Items.Count - max} more"), $"; …{shop.Items.Count - max} more") : "");
    }

    private (bool Ok, string Result) SetMerchant(string? chatId, JsonObject args)
    {
        if (chatId is null)
        {
            return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        }

        var name = (Arg(args, "npc") ?? "").Trim();
        if (name.Length == 0)
        {
            return (false, L("Ошибка: не указано имя торговца (npc).", "Error: the merchant name (npc) is not given."));
        }

        var state = _rpg.GetOrCreate(chatId);
        var npc = FindNpc(state, name);
        if (npc is null)
        {
            npc = new ImportantNpc { Name = name, Attitude = L("нейтральное", "neutral"), Category = "nearby" };
            state.ImportantCharacters.Add(npc);
        }
        else
        {
            state.Notables.Remove(npc);
            if (!state.ImportantCharacters.Contains(npc)) state.ImportantCharacters.Add(npc);
            npc.Category = "nearby";
        }

        // Торговец торгует здесь и сейчас — привязка всегда к текущему месту сцены: переехавший
        // следом за героем не останется приклеен к прежнему городу (см. RpgState.NearbyHere).
        npc.NearbyPlace = state.ScenePlace ?? "";

        var isNew = npc.Shop is null;
        var shop = npc.Shop ??= new MerchantShop();

        if (Arg(args, "kind") is { Length: > 0 } kindRaw)
        {
            var kind = ParseKind(kindRaw);
            if (kind is null)
            {
                if (isNew)
                {
                    npc.Shop = null;
                }

                return (false, L($"Ошибка: неизвестный тип торговца «{kindRaw}». Доступны: ", $"Error: unknown merchant type \"{kindRaw}\". Available: ") +
                               string.Join(", ", ItemEconomy.Merchants.Select(m => m.Key)) + ".");
            }

            if (isNew || shop.Kind != kind.Key)
            {
                shop.Kind = kind.Key;
                shop.Markup = kind.Markup;
                shop.BuyRate = kind.BuyRate;
                shop.OffRate = kind.OffRate;
            }
        }
        else if (isNew)
        {
            var kind = ItemEconomy.Merchant(npc.Role.Length > 0 ? ParseKind(npc.Role)?.Key : null);
            (shop.Kind, shop.Markup, shop.BuyRate, shop.OffRate) = (kind.Key, kind.Markup, kind.BuyRate, kind.OffRate);
        }

        if (Arg(args, "title") is { Length: > 0 } title)
        {
            shop.Title = title.Trim();
        }

        if (ParseTier(Arg(args, "tier")) is { } tier)
        {
            shop.Tier = tier;
        }

        if (OptDouble(args, "markup") is { } markup)
        {
            shop.Markup = Math.Clamp(markup, 0.5, 3.0);
        }

        if (OptDouble(args, "buy_rate") is { } buyRate)
        {
            shop.BuyRate = Math.Clamp(buyRate, 0.05, 0.95);
        }

        if (OptDouble(args, "off_rate") is { } offRate)
        {
            shop.OffRate = Math.Clamp(offRate, 0, 0.6);
        }

        var warnings = new List<string>();
        if (Arg(args, "buys") is { Length: > 0 } buysRaw)
        {
            var (ok, bad) = ParseCategories(buysRaw);
            shop.Buys = ok;
            warnings.AddRange(bad.Select(b => L($"категория «{b}» не существует", $"the category \"{b}\" does not exist")));
        }

        if (Arg(args, "also") is { Length: > 0 } alsoRaw)
        {
            var (ok, bad) = ParseCategories(alsoRaw);
            shop.Also = ok;
            warnings.AddRange(bad.Select(b => L($"категория «{b}» не существует", $"the category \"{b}\" does not exist")));
        }

        if (OptInt(args, "gold", int.MinValue) is var gold && gold != int.MinValue)
        {
            shop.Gold = Math.Max(0, gold);
        }
        else if (isNew)
        {
            shop.Gold = ItemEconomy.RollGold(shop.Tier);
        }

        if (isNew || OptBool(args, "restock"))
        {
            // Сюжетные вещи (эпические и легендарные) переживают завоз товара.
            var keep = shop.Items.Where(i => ItemEconomy.RarityKey(i.Rarity) is "epic" or "legendary").ToList();
            var fresh = _economy.GenerateStock(shop);
            foreach (var special in keep)
            {
                if (InventoryOps.FindFree(fresh, special.W, special.H, null, null, null, MerchantShop.ShopCols, MerchantShop.ShopRows) is { } spot)
                {
                    (special.Col, special.Row) = spot;
                    fresh.Add(special);
                }
            }

            shop.Items = fresh;
        }

        _rpg.Save(chatId, state);
        var kindTitle = ItemEconomy.Merchant(shop.Kind).Title;
        return (true, L($"OK: {(isNew ? "открыта лавка" : "обновлена лавка")} «{npc.Name}» ({kindTitle}, {shop.Tier}), у торговца {shop.Gold} з. " +
                           $"Товары и цены для героя: {ShopSummary(shop, state.Character)}.",
                           $"OK: {(isNew ? "opened the shop" : "updated the shop")} \"{npc.Name}\" ({kindTitle}, {shop.Tier}), the merchant has {shop.Gold} {Genre.Coin}. " +
                           $"Goods and prices for the hero: {ShopSummary(shop, state.Character)}.") +
                           (warnings.Count > 0 ? L(" Внимание: ", " Warning: ") + string.Join("; ", warnings) + "." : ""));
    }

    private (bool Ok, string Result) UpdateShopItem(string? chatId, JsonObject args)
    {
        if (chatId is null)
        {
            return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        }

        var state = _rpg.GetOrCreate(chatId);
        var npcName = (Arg(args, "npc") ?? "").Trim();
        var npc = FindNpc(state, npcName);
        if (npc?.Shop is not { } shop)
        {
            return (false, L($"Ошибка: у «{npcName}» нет лавки — сначала set_merchant.", $"Error: \"{npcName}\" has no shop — call set_merchant first."));
        }

        var name = (Arg(args, "name") ?? "").Trim();
        if (name.Length == 0)
        {
            return (false, L("Ошибка: не указано название товара (name).", "Error: the item name (name) is not given."));
        }

        var item = shop.Items.FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var qty = OptInt(args, "quantity", int.MinValue);
        if (OptBool(args, "remove") || qty == 0)
        {
            if (item is null)
            {
                return (false, L($"Ошибка: товара «{name}» в лавке нет.", $"Error: there is no \"{name}\" in the shop."));
            }

            shop.Items.Remove(item);
            _rpg.Save(chatId, state);
            return (true, L($"OK: «{name}» убран из лавки «{npc.Name}».", $"OK: \"{name}\" is removed from the shop of \"{npc.Name}\"."));
        }

        var created = item is null;
        if (item is null)
        {
            var def = _catalog.Resolve(Arg(args, "icon"), name);
            item = new GridItem
            {
                Name = name,
                Icon = def.Id,
                W = def.W,
                H = def.H,
                Slot = def.EquipSlot,
                TwoHanded = def.TwoHanded,
            };
        }
        else if (_catalog.Get(Arg(args, "icon")) is { } newDef)
        {
            (item.Icon, item.W, item.H, item.Slot, item.TwoHanded) = (newDef.Id, newDef.W, newDef.H, newDef.EquipSlot, newDef.TwoHanded);
        }

        if (qty > 0)
        {
            item.Quantity = qty;
        }

        if (OptInt(args, "price", 0) is var price && price > 0)
        {
            item.Value = price;
        }

        if (Arg(args, "rarity") is { Length: > 0 } rarity)
        {
            item.Rarity = ItemEconomy.RarityKey(rarity);
        }

        if (Arg(args, "note") is { Length: > 0 } note)
        {
            item.Note = note.Trim();
        }
        else if (created && item.Note.Length == 0)
        {
            item.Note = ItemEconomy.RarityNote(item.Rarity);
        }

        var statsNote = ApplyItemStats(item, args, Progression.ParseLevel(state.Character.Level));

        if (created)
        {
            var spot = InventoryOps.FindFree(shop.Items, item.W, item.H, null, null, null, MerchantShop.ShopCols, MerchantShop.ShopRows);
            if (spot is null)
            {
                return (false, L($"Ошибка: в лавке «{npc.Name}» нет места под {item.W}x{item.H}. Убери что-нибудь (update_shop_item remove).", $"Error: the shop of \"{npc.Name}\" has no {item.W}x{item.H} room. Remove something (update_shop_item remove)."));
            }

            (item.Col, item.Row) = spot.Value;
            shop.Items.Add(item);
        }

        _rpg.Save(chatId, state);
        return (true, L($"OK: {(created ? "в лавку добавлен" : "обновлён")} товар «{item.Name}» x{item.Quantity}, " +
                       $"цена для героя {_economy.BuyPrice(shop, item, state.Character)} з. ({ItemEconomy.RarityTitle(item.Rarity)}).",
                       $"OK: {(created ? "added to the shop" : "updated")} the item \"{item.Name}\" x{item.Quantity}, " +
                       $"price for the hero {_economy.BuyPrice(shop, item, state.Character)} {Genre.Coin} ({ItemEconomy.RarityTitle(item.Rarity)}).") + statsNote);
    }

    private (bool Ok, string Result) OpenTrade(string? chatId, JsonObject args)
    {
        if (chatId is null)
        {
            return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        }

        var state = _rpg.GetOrCreate(chatId);
        var npcName = (Arg(args, "npc") ?? "").Trim();
        var npc = FindNpc(state, npcName);
        if (npc?.Shop is not { } shop)
        {
            var merchants = state.ImportantCharacters.Where(n => n.Shop is not null).Select(n => n.Name).ToList();
            return (false, L($"Ошибка: у «{npcName}» нет лавки. ", $"Error: \"{npcName}\" has no shop. ") +
                               (merchants.Count > 0 ? L("Торговцы: ", "Merchants: ") + string.Join(", ", merchants) + "." : L("Сначала создай её через set_merchant.", "Create it via set_merchant first.")));
        }

        return (true, L($"OK: игроку открыто окно лавки «{npc.Name}». Товары: {ShopSummary(shop, state.Character, 15)}. " +
                      "Жди итога сделки в следующем сообщении игрока.",
                      $"OK: the shop window of \"{npc.Name}\" is open for the player. Goods: {ShopSummary(shop, state.Character, 15)}. " +
                      "Wait for the deal outcome in the player's next message."));
    }


    private (bool Ok, string Result) UpdateAdversary(string? chatId, JsonObject args)
    {
        if (chatId is null)
        {
            return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        }

        var name = (Arg(args, "name") ?? "").Trim();
        if (name.Length == 0)
        {
            return (false, L("Ошибка: не указано имя противника (name).", "Error: the adversary name (name) is not given."));
        }

        var state = _rpg.GetOrCreate(chatId);
        // Жив ли бой до этого вызова — чтобы заметить последний добивающий удар.
        var fightingBefore = state.InCombat();

        if (OptBool(args, "remove"))
        {
            var removed = state.Adversaries.RemoveAll(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                return (false, L($"Ошибка: противник «{name}» не найден.", $"Error: the adversary \"{name}\" was not found."));
            }

            _rpg.Save(chatId, state);
            return (true, L($"OK: противник «{name}» удалён из списка.", $"OK: the adversary \"{name}\" is removed from the list."));
        }

        var adv = state.Adversaries.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var created = adv is null;
        if (adv is null)
        {
            adv = new Adversary { Name = name };
            state.Adversaries.Add(adv);
        }

        if (Arg(args, "level") is { } lvl && lvl.Trim().Length > 0)
        {
            adv.Level = lvl.Trim();
        }

        if (OptInt(args, "hp_current", int.MinValue) is var hpCur && hpCur != int.MinValue)
        {
            adv.HpCurrent = Math.Max(0, hpCur);
        }
        if (OptInt(args, "hp_delta", 0) is var hpDelta && hpDelta != 0)
        {
            if (hpDelta < 0 && adv.Shield > 0) { var absorbed = Math.Min(adv.Shield, -hpDelta); adv.Shield -= absorbed; hpDelta += absorbed; }
            adv.HpCurrent = Math.Clamp(adv.HpCurrent + hpDelta, 0, Math.Max(adv.HpMax, adv.HpCurrent));
        }

        if (OptInt(args, "hp_max", 0) is var hpMax && hpMax > 0)
        {
            adv.HpMax = hpMax;
            if (adv.HpCurrent > hpMax)
            {
                adv.HpCurrent = hpMax;
            }
        }

        if (Arg(args, "status") is { } st && st.Trim().Length > 0)
        {
            adv.Status = st.Trim();
        }
        if (OptInt(args, "shield", int.MinValue) is var shield && shield != int.MinValue) adv.Shield = Math.Max(0, shield);
        var threat = (Arg(args, "threat_tier") ?? "").Trim().ToLowerInvariant();
        if (threat is "minion" or "ordinary" or "elite" or "quest_boss" or "dungeon_boss" or "arc_boss") adv.ThreatTier = threat;
        if (Str(args, "archetype") is { Length: > 0 } arch) adv.Archetype = Progression.NormalizeMonsterArchetype(arch);
        if (Str(args, "kind") is { Length: > 0 } kindArg) adv.Kind = kindArg.Trim().ToLowerInvariant();
        if (Num(args, "stagger") is { } stagger) adv.Stagger = Math.Clamp(stagger, 0, Math.Max(adv.StaggerMax, stagger));

        // Портрет: выбор модели, если он похож на существо; иначе — по имени (волк не получит облик гоблина).
        var notes = new List<string>();
        if (created || Arg(args, "portrait") is not null)
        {
            var (resolved, portraitNote) = PortraitCatalog.ResolveEnemy(Arg(args, "portrait"), adv.Name, adv.ThreatTier);
            adv.Portrait = resolved?.Id ?? (Arg(args, "portrait") ?? "").Trim();
            if (adv.Kind.Length == 0 && resolved is not null) adv.Kind = resolved.Kind;
            if (portraitNote.Length > 0) notes.Add(portraitNote);
        }

        // Новый противник без чисел — статблок по уровню и роли, чтобы бой не был ни пустяком, ни мясорубкой.
        if (created && adv.HpMax <= 0)
        {
            var statLevel = adv.Level.Length > 0 ? Progression.ParseLevel(adv.Level) : Progression.ParseLevel(state.Character.Level);
            var block = Progression.Monster(statLevel, adv.ThreatTier, adv.Archetype);
            ApplyBlock(adv, block, DefaultDamageType(adv.Kind, block.Archetype));
            notes.Add(L($"статблок по уровню {statLevel}: {BlockText(adv)} (для нового боя удобнее plan_encounter — он оценит сложность)", $"stat block for level {statLevel}: {BlockText(adv)} (for a new fight plan_encounter is handier — it estimates the difficulty)"));
        }
        else if (created && adv.Ac == 0)
        {
            var statLevel = adv.Level.Length > 0 ? Progression.ParseLevel(adv.Level) : Progression.ParseLevel(state.Character.Level);
            var block = Progression.Monster(statLevel, adv.ThreatTier, adv.Archetype);
            var hp = adv.HpMax;
            ApplyBlock(adv, block, DefaultDamageType(adv.Kind, block.Archetype));
            adv.HpMax = adv.HpCurrent = hp;
            if (hp > block.Hp * 2 || hp * 2 < block.Hp)
            {
                notes.Add(L($"ВНИМАНИЕ: ХП {hp} вне баланса для роли «{Progression.RoleTitle(block.Role)}» ур. {statLevel} (норма ~{block.Hp}) — бой будет {(hp > block.Hp ? "затяжным" : "слишком лёгким")}", $"WARNING: HP {hp} is out of balance for the role \"{Progression.RoleTitle(block.Role)}\" lvl {statLevel} (normal ~{block.Hp}) — the fight will be {(hp > block.Hp ? "drawn out" : "too easy")}"));
            }
            notes.Add(L($"остальные числа по уровню: КБ {adv.Ac}, атака {adv.AttackBonus:+#;-#;+0}, урон {adv.Damage}", $"the other numbers by level: AC {adv.Ac}, attack {adv.AttackBonus:+#;-#;+0}, damage {adv.Damage}"));
        }
        if (Str(args, "add_effect") is { } addEffect && addEffect.Trim().Length > 0) CombatEffects.AddOrRefresh(adv.Effects, addEffect, Num(args, "rounds"), true);
        if (Str(args, "remove_effect") is { } removeEffect) CombatEffects.Remove(adv.Effects, removeEffect);
        if (Str(args, "morale") is { Length: > 0 } moraleArg)
        {
            adv.Morale = moraleArg.Trim().ToLowerInvariant() switch
            {
                "fled" or "flee" or "fleeing" or "бежал" or "бегство" => "fled",
                "surrendered" or "surrender" or "yielded" or "сдался" or "сдаётся" => "surrendered",
                _ => "",
            };
        }

        // После урона — честная проверка морали: раненые и оставшиеся без вожака могут бежать или сдаться.
        if (hpDelta < 0 || args["hp_current"] is not null)
        {
            var moraleEvents = new List<CombatEvent>();
            notes.AddRange(CombatRules.CheckMorale(state, moraleEvents));
            CombatEvents.Push(chatId, moraleEvents);
        }

        if (Arg(args, "abilities") is { } ab && ab.Trim().Length > 0)
        {
            adv.Abilities = ab.Trim();
        }

        if (Arg(args, "notes") is { } nt && nt.Trim().Length > 0)
        {
            adv.Notes = nt.Trim();
        }

        // Добивший последнего противника ход: без явного напоминания модель часто заканчивает
        // его текстом и в чате не появляются кнопки выбора следующего действия.
        var justWon = fightingBefore && !state.InCombat();
        var cleanup = justWon ? CombatRules.EndCombat(state) : "";

        _rpg.Save(chatId, state);
        return (true, L($"OK: {(created ? "добавлен" : "обновлён")} противник «{adv.Name}» ({adv.HpCurrent}/{adv.HpMax} HP).", $"OK: {(created ? "added" : "updated")} the adversary \"{adv.Name}\" ({adv.HpCurrent}/{adv.HpMax} HP).") +
                      (notes.Count > 0 ? " " + string.Join("; ", notes) + "." : "") +
                      (justWon
                          ? L($" Все противники выбыли — бой окончен. {cleanup}Вызови award_xp encounter=true и roll_loot, назови итог и заверши ход через offer_choices (обыск тел, допрос сдавшихся, лечение, что делать дальше).", $" All adversaries are out — the fight is over. {cleanup}Call award_xp encounter=true and roll_loot, state the outcome and finish the turn with offer_choices (searching the bodies, questioning the surrendered, healing, what to do next).")
                          : ""));
    }

    private (bool Ok, string Result) UpdateCity(string? chatId, JsonObject args)
    {
        if (chatId is null)
        {
            return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        }

        var name = (Arg(args, "name") ?? "").Trim();
        if (name.Length == 0)
        {
            return (false, L("Ошибка: не указано название города (name).", "Error: the town name (name) is not given."));
        }

        var state = _rpg.GetOrCreate(chatId);
        var city = state.Cities.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        // Удаление не должно создавать пустой город при опечатке в имени.
        if (Arg(args, "remove_poi") is { } removeName && removeName.Trim().Length > 0)
        {
            if (city is null)
            {
                return (false, L($"Ошибка: город «{name}» не найден.", $"Error: the town \"{name}\" was not found."));
            }

            var removed = city.Points.RemoveAll(p => p.Name.Equals(removeName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                return (false, L($"Ошибка: точка «{removeName.Trim()}» не найдена в городе «{city.Name}».", $"Error: the point \"{removeName.Trim()}\" was not found in the town \"{city.Name}\"."));
            }

            _rpg.Save(chatId, state);
            return (true, L($"OK: в городе «{city.Name}» удалена точка «{removeName.Trim()}».", $"OK: the point \"{removeName.Trim()}\" is removed from the town \"{city.Name}\"."));
        }

        var created = city is null;
        if (city is null)
        {
            city = new CityRecord { Name = name };
            state.Cities.Add(city);
        }

        if (Arg(args, "description") is { } d && d.Trim().Length > 0)
        {
            city.Description = d.Trim();
        }

        if (args["is_major"] is JsonValue majorValue && majorValue.TryGetValue<bool>(out var isMajor))
        {
            city.IsMajor = isMajor;
            if (isMajor)
            {
                city.AdventureGuild ??= new AdventureGuild { Name = (Lang.IsEn ? Genre.Pick($"Adventurers' Guild — {city.Name}", $"Fixer — {city.Name}", $"Middleman — {city.Name}") : Genre.Pick($"Гильдия приключенцев — {city.Name}", $"Фиксер — {city.Name}", $"Посредник — {city.Name}")) };
                if (!city.Points.Any(p => p.Type.Contains("гильд", StringComparison.OrdinalIgnoreCase) || p.Type.Contains("guild", StringComparison.OrdinalIgnoreCase)))
                    city.Points.Add(new PoiRecord { Name = city.AdventureGuild.Name, Type = L("гильдия приключенцев", "adventurers' guild"), Note = L("Доска поручений, регистрация ранга и выдача наград.", "The job board, rank registration and rewards.") });
            }
        }

        var poiName = (Arg(args, "poi_name") ?? "").Trim();
        if (poiName.Length > 0)
        {
            var poi = city.Points.FirstOrDefault(p => p.Name.Equals(poiName, StringComparison.OrdinalIgnoreCase));
            if (poi is null)
            {
                poi = new PoiRecord { Name = poiName };
                city.Points.Add(poi);
            }

            if (Arg(args, "poi_type") is { } pt && pt.Trim().Length > 0)
            {
                poi.Type = pt.Trim();
            }

            if (Arg(args, "poi_note") is { } pn && pn.Trim().Length > 0)
            {
                poi.Note = pn.Trim();
            }
        }

        var acquaintanceName = (Arg(args, "acquaintance") ?? "").Trim();
        if (acquaintanceName.Length > 0)
        {
            var local = city.Acquaintances.FirstOrDefault(a => a.Name.Equals(acquaintanceName, StringComparison.OrdinalIgnoreCase));
            if (local is null)
            {
                local = new LocalAcquaintance { Name = acquaintanceName };
                city.Acquaintances.Add(local);
            }

            if (Arg(args, "acquaintance_role") is { } ar) local.Role = ar.Trim();
            if (Arg(args, "acquaintance_note") is { } an) local.Note = an.Trim();
        }

        _rpg.Save(chatId, state);
        var desc = poiName.Length > 0 ? L($", точка «{poiName}»", $", point \"{poiName}\"") : "";
        return (true, L($"OK: {(created ? "создан" : "обновлён")} город «{city.Name}»{desc}.", $"OK: {(created ? "created" : "updated")} the town \"{city.Name}\"{desc}."));
    }

    private (bool Ok, string Result) UpdateBattleLoot(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var name=(Arg(args,"name")??"").Trim(); if(name.Length==0)return(false,L("Ошибка: нужно name.", "Error: name is required."));
        if (Regex.IsMatch(name, @"(?i)(золото|монет|gold|coins?)")) return(false,L("Ошибка: золото не бывает предметом добычи; обнови счётчик gold при финализации.", "Error: gold is never a loot item; update the gold counter at finalization."));
        var state=_rpg.GetOrCreate(chatId); var def=_catalog.Resolve(Str(args,"icon"),name);
        var item=new GridItem{Name=name,Icon=def.Id,W=def.W,H=def.H,Slot=def.EquipSlot,TwoHanded=def.TwoHanded,Quantity=Math.Max(1,Num(args,"quantity")??1),Note=(Str(args,"note")??"").Trim(),Value=Math.Max(0,Num(args,"value")??0)};
        var (rarityNote, _) = ApplyRarity(state, item, Str(args,"rarity"));
        var statsNote = ApplyItemStats(item, args, Progression.ParseLevel(state.Character.Level));
        state.BattleLoot.Add(item); _rpg.Save(chatId,state); return(true,L($"OK: «{name}» добавлен в послебоевое окно добычи; в сумку ещё не применён.", $"OK: \"{name}\" is added to the post-battle loot window; not yet applied to the bag.")+rarityNote+statsNote);
    }

    private (bool Ok, string Result) UpdateGuildJob(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var cityName = (Arg(args, "city") ?? "").Trim();
        var name = (Arg(args, "name") ?? "").Trim();
        if (cityName.Length == 0 || name.Length == 0) return (false, L("Ошибка: нужны city и name.", "Error: city and name are required."));
        var state = _rpg.GetOrCreate(chatId);
        var city = state.Cities.FirstOrDefault(c => c.Name.Equals(cityName, StringComparison.OrdinalIgnoreCase));
        if (city is null || !city.IsMajor) return (false, L($"Ошибка: «{cityName}» не зарегистрирован как крупный город.", $"Error: \"{cityName}\" is not registered as a major city."));
        city.AdventureGuild ??= new AdventureGuild { Name = (Lang.IsEn ? Genre.Pick($"Adventurers' Guild — {city.Name}", $"Fixer — {city.Name}", $"Middleman — {city.Name}") : Genre.Pick($"Гильдия приключенцев — {city.Name}", $"Фиксер — {city.Name}", $"Посредник — {city.Name}")) };
        var job = city.AdventureGuild.Jobs.FirstOrDefault(j => j.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var created = job is null;
        if (job is null) { job = new GuildJob { Name = name }; city.AdventureGuild.Jobs.Add(job); }
        if (Arg(args, "description") is { } d) job.Description = d.Trim();
        var rank = (Arg(args, "min_rank") ?? job.MinRank).Trim().ToUpperInvariant();
        if (GuildRanks.All.Any(x => x.Rank == rank)) job.MinRank = rank;
        if (Arg(args, "difficulty") is { } difficulty) job.Difficulty = difficulty.Trim();
        if (args["gold_reward"] is not null) job.GoldReward = Math.Max(0, OptInt(args, "gold_reward", job.GoldReward));
        if (args["reputation_reward"] is not null) job.ReputationReward = Math.Max(0, OptInt(args, "reputation_reward", job.ReputationReward));
        if (Arg(args, "item_reward") is { } item) job.ItemReward = item.Trim();
        var status = (Arg(args, "state") ?? "").Trim().ToLowerInvariant();
        if (status is "available" or "accepted" or "completed" or "expired") job.State = status;
        _rpg.Save(chatId, state);
        return (true, L($"OK: {(created ? "добавлено" : "обновлено")} гильдейское задание «{job.Name}» (ранг {job.MinRank}, {job.GoldReward} з., +{job.ReputationReward} реп.).", $"OK: {(created ? "added" : "updated")} the guild job \"{job.Name}\" (rank {job.MinRank}, {job.GoldReward} {Genre.Coin}, +{job.ReputationReward} rep.)."));
    }

    private (bool Ok, string Result) UpdateGuildReputation(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        if (args["reputation"] is null) return (false, L("Ошибка: нужна итоговая reputation.", "Error: the total reputation is required."));
        var state = _rpg.GetOrCreate(chatId);
        var before = state.Character.GuildReputation;
        state.Character.GuildReputation = Math.Max(0, OptInt(args, "reputation", before));
        _rpg.Save(chatId, state);
        return (true, L($"OK: репутация гильдии {before} → {state.Character.GuildReputation}; ранг {GuildRanks.TitleFor(state.Character.GuildReputation)}. Причина: {(Arg(args, "reason") ?? "не указана").Trim()}.", $"OK: guild reputation {before} → {state.Character.GuildReputation}; rank {GuildRanks.TitleFor(state.Character.GuildReputation)}. Reason: {(Arg(args, "reason") ?? "not given").Trim()}."));
    }

    private (bool Ok, string Result) OpenAdventureGuild(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var cityName = (Arg(args, "city") ?? "").Trim();
        var state = _rpg.GetOrCreate(chatId);
        var city = state.Cities.FirstOrDefault(c => c.Name.Equals(cityName, StringComparison.OrdinalIgnoreCase));
        if (city?.AdventureGuild is null || !city.IsMajor) return (false, L(
            $"Ошибка: в «{cityName}» нет отделения гильдии. Не останавливай ход: сообщи игроку это в повествовании и предложи другие действия; не повторяй open_adventure_guild для этого места.",
            $"Error: \"{cityName}\" has no guild branch. Do not stop the turn: tell the player in the narrative and offer other actions; do not retry open_adventure_guild for this place."));
        var count = city.AdventureGuild.Jobs.Count(j => j.State == "available" && GuildRanks.CanTake(state.Character.GuildReputation, j.MinRank));
        return (true, L($"OK: открыто меню гильдии «{city.AdventureGuild.Name}». Ранг героя {GuildRanks.TitleFor(state.Character.GuildReputation)}, доступно заданий: {count}.", $"OK: the guild menu \"{city.AdventureGuild.Name}\" is open. The hero's rank {GuildRanks.TitleFor(state.Character.GuildReputation)}, jobs available: {count}."));
    }

    private (bool Ok, string Result) UpdateQuest(string? chatId, JsonObject args)
    {
        if (chatId is null)
        {
            return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        }

        var name = (Arg(args, "name") ?? "").Trim();
        if (name.Length == 0)
        {
            return (false, L("Ошибка: не указано название задания (name).", "Error: the quest title (name) is not given."));
        }

        var state = _rpg.GetOrCreate(chatId);
        var quest = state.Quests.FirstOrDefault(q => q.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var created = quest is null;
        if (quest is null)
        {
            quest = new QuestRecord { Name = name, State = "accepted" };
            state.Quests.Add(quest);
        }

        if (Arg(args, "description") is { } d && d.Trim().Length > 0)
        {
            quest.Description = d.Trim();
        }

        if (Arg(args, "giver") is { } g && g.Trim().Length > 0)
        {
            quest.Giver = g.Trim();
        }

        if (Arg(args, "reward") is { } r && r.Trim().Length > 0)
        {
            quest.Reward = r.Trim();
        }

        if (Arg(args, "progress") is { } p && p.Trim().Length > 0)
        {
            quest.Progress.Add(p.Trim());
        }

        var st = (Arg(args, "state") ?? "").Trim().ToLowerInvariant();
        if (st is "accepted" or "done" or "failed")
        {
            quest.State = st;
        }

        _rpg.Save(chatId, state);
        return (true, L($"OK: задание «{quest.Name}» {(created ? "принято" : "обновлено")} [{quest.State}]", $"OK: the quest \"{quest.Name}\" is {(created ? "accepted" : "updated")} [{quest.State}]") +
                       (quest.Progress.Count > 0 ? L("; шагов: ", "; steps: ") + quest.Progress.Count : "") + ".");
    }

    private (bool Ok, string Result) UpdateArc(string? chatId, JsonObject args)
    {
        if (chatId is null)
        {
            return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        }

        var name = (Arg(args, "name") ?? "").Trim();
        if (name.Length == 0)
        {
            return (false, L("Ошибка: не указано название арки (name).", "Error: the arc title (name) is not given."));
        }

        var state = _rpg.GetOrCreate(chatId);
        var arc = state.StoryArcs.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var created = arc is null;
        var arcReward = "";
        var switched = new List<string>();
        if (arc is null)
        {
            arc = new StoryArc { Name = name, State = "active" };
            state.StoryArcs.Add(arc);
        }

        if (Arg(args, "premise") is { } p && p.Trim().Length > 0)
        {
            arc.Premise = p.Trim();
        }

        if (Arg(args, "antagonist") is { } an && an.Trim().Length > 0)
        {
            arc.Antagonist = an.Trim();
        }

        if (Arg(args, "next_lead") is { } lead && lead.Trim().Length > 0)
        {
            arc.NextLead = lead.Trim();
        }

        var happened = Arg(args, "add_event") ?? Arg(args, "add_beat");
        if (happened is { } beat && beat.Trim().Length > 0)
        {
            arc.Events.Add(beat.Trim());
        }

        var st = (Arg(args, "state") ?? (created ? "active" : "")).Trim().ToLowerInvariant();
        if (st is "active" or "done" or "abandoned")
        {
            // Старые сохранения не знали этот флаг. Уже завершённая арка считается награждённой,
            // если её вновь открывают: повторный цикл active → done не должен печатать легендарки.
            if (st == "active" && arc.State.Equals("done", StringComparison.OrdinalIgnoreCase))
            {
                arc.CompletionRewardGiven = true;
            }

            // Только одна активная арка. Переключение не равно победному финалу: прежняя линия
            // считается оставленной и не выдаёт разрешение на легендарную награду.
            if (st == "active")
            {
                foreach (var other in state.StoryArcs.Where(a => !ReferenceEquals(a, arc)))
                {
                    if (other.State.Equals("active", StringComparison.OrdinalIgnoreCase))
                    {
                        other.State = "abandoned";
                        switched.Add(other.Name);
                    }
                }
            }

            // Завершённая арка — кульминация: разрешает одну легендарную награду.
            if (st == "done" && !arc.State.Equals("done", StringComparison.OrdinalIgnoreCase) && !arc.CompletionRewardGiven)
            {
                state.LegendaryTokens++;
                arc.CompletionRewardGiven = true;
                arcReward = L(" Арка завершена: можно выдать ОДНУ легендарную награду за финал (update_grid_item/equip_item с rarity=legendary) — если она уместна в сюжете.", " The arc is complete: ONE legendary reward for the finale may be handed out (update_grid_item/equip_item with rarity=legendary) — if it fits the story.");
            }

            arc.State = st;
        }

        _rpg.Save(chatId, state);
        return (true, L($"OK: арка «{arc.Name}» {(created ? "создана" : "обновлена")} [{arc.State}]", $"OK: the arc \"{arc.Name}\" is {(created ? "created" : "updated")} [{arc.State}]") +
                       (arc.Events.Count > 0 ? L("; событий в журнале: ", "; logged events: ") + arc.Events.Count : "") + "." +
                       (switched.Count > 0 ? L(" Прежняя активная арка оставлена без награды: ", " The previous active arc is left without a reward: ") + string.Join(", ", switched) + "." : "") + arcReward);
    }

    private (bool Ok, string Result) BuyRumors(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var source = (Arg(args, "source") ?? "").Trim();
        var linkedTo = (Arg(args, "linked_to") ?? "").Trim();
        var linkedClue = (Arg(args, "linked_clue") ?? "").Trim();
        if (source.Length == 0 || linkedTo.Length == 0 || linkedClue.Length == 0)
            return (false, L("Ошибка: нужны source, linked_to и linked_clue.", "Error: source, linked_to and linked_clue are required."));

        var state = _rpg.GetOrCreate(chatId);
        var isCurrent = state.StoryArcs.Any(a => a.State == "active" && a.Name.Equals(linkedTo, StringComparison.OrdinalIgnoreCase)) ||
                        state.Quests.Any(q => q.State == "accepted" && q.Name.Equals(linkedTo, StringComparison.OrdinalIgnoreCase));
        if (!isCurrent)
            return (false, L($"Ошибка: «{linkedTo}» не является активной аркой или принятым заданием. Один слух обязан вести к текущей линии.", $"Error: \"{linkedTo}\" is not the active arc or an accepted quest. One rumor must lead to the current line."));

        var cost = Math.Max(0, OptInt(args, "cost", 0));
        if (state.Character.Gold < cost) return (false, L($"Ошибка: слухи стоят {cost} з., у героя только {state.Character.Gold} з.", $"Error: the rumors cost {cost} {Genre.Coin}, the hero has only {state.Character.Gold} {Genre.Coin}."));
        var count = Math.Clamp(OptInt(args, "count", 3), 2, 5);
        var pools = new Dictionary<string, List<string>>
        {
            ["minor"] = SplitPool(Arg(args, "minor_rumors")),
            ["medium"] = SplitPool(Arg(args, "medium_quests")),
            ["long"] = SplitPool(Arg(args, "long_quests")),
        };
        if (pools.Values.Any(x => x.Count == 0)) return (false, L("Ошибка: для честного колеса нужны кандидаты всех трёх категорий.", "Error: a fair wheel needs candidates of all three categories."));

        state.Character.Gold -= cost;
        var added = new List<RumorClue>
        {
            new() { Source = source, LinkedTo = linkedTo, Summary = linkedClue, Tier = "linked" },
        };
        var rolls = new List<string>();
        var wheelSpins = new List<RumorWheel.Spin>();
        for (var i = 1; i < count; i++)
        {
            var roll = Random.Shared.Next(1, 101);
            var tier = roll <= 65 ? "minor" : roll <= 92 ? "medium" : "long";
            var pool = pools[tier];
            var clue = new RumorClue { Source = source, LinkedTo = L("побочная зацепка", "side lead"), Summary = pool[Random.Shared.Next(pool.Count)], Tier = tier };
            added.Add(clue);
            rolls.Add($"d100={roll}: {(tier == "minor" ? L("малая зацепка", "small lead") : tier == "medium" ? L("средний квест", "medium quest") : L("длинный квест / возможный компаньон", "long quest / possible companion"))}");
            wheelSpins.Add(RumorWheel.MakeSpin(roll, tier));
        }

        state.RumorClues.AddRange(added);
        _rpg.Save(chatId, state);
        var selectedForMaster = string.Join("; ", added.Skip(1).Select(r => $"[{r.Tier}] {r.Summary}"));
        return (true, RumorWheel.BuildMeta(source, wheelSpins) + "\n" +
                      L($"OK: куплено слухов: {count}; золото −{cost}. Один слух связан с «{linkedTo}». Колесо остальных: {string.Join("; ", rolls)}. " +
            $"Выбранное содержание для мастера: {selectedForMaster}. " +
            "Содержание сохранено. Не выдавай его напрямую: когда оно станет уместно, предложи вариант «Попробовать вспомнить, что говорил(а) " + source + "».",
            $"OK: rumors bought: {count}; gold −{cost}. One rumor is linked to \"{linkedTo}\". The wheel for the rest: {string.Join("; ", rolls)}. " +
            $"The chosen content for the master: {selectedForMaster}. " +
            "The content is saved. Do not reveal it directly: when it becomes relevant, offer the option \"Try to remember what " + source + " said\"."));
    }

    private static List<string> SplitPool(string? raw) => (raw ?? "").Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

    private (bool Ok, string Result) RecallRumor(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var source = (Arg(args, "source") ?? "").Trim();
        var linked = (Arg(args, "linked_to") ?? "").Trim();
        var state = _rpg.GetOrCreate(chatId);
        var clue = state.RumorClues.LastOrDefault(r => !r.Recalled &&
            (source.Length == 0 || r.Source.Equals(source, StringComparison.OrdinalIgnoreCase)) &&
            (linked.Length == 0 || r.LinkedTo.Equals(linked, StringComparison.OrdinalIgnoreCase)));
        if (clue is null) return (false, L("Ошибка: подходящей невспомненной подсказки нет.", "Error: there is no matching unrecalled hint."));
        clue.Recalled = true;
        _rpg.Save(chatId, state);
        return (true, L($"Точная запись для мастера: {clue.Summary}. Источник: {clue.Source}. НЕ цитируй точно: передай обрывочно и расплывчато, будто герой забыл детали.", $"The exact record for the master: {clue.Summary}. Source: {clue.Source}. Do NOT quote it exactly: convey it in fragments and vaguely, as if the hero forgot the details."));
    }

    private (bool Ok, string Result) UpdatePartyMember(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var name = (Arg(args, "name") ?? "").Trim();
        if (name.Length == 0) return (false, L("Ошибка: не указано имя участника.", "Error: the member name is not given."));
        var state = _rpg.GetOrCreate(chatId);
        var member = state.Party.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (OptBool(args, "remove"))
        {
            if (member is null)
            {
                return (false, L($"Ошибка: спутник «{name}» не найден в группе.", $"Error: the companion \"{name}\" was not found in the party."));
            }

            state.Party.Remove(member);
            var departure = (Arg(args, "departure") ?? "normal").Trim().ToLowerInvariant();
            if (departure == "normal")
            {
                var npc = state.ImportantCharacters.FirstOrDefault(n => n.Name.Equals(member.Name, StringComparison.OrdinalIgnoreCase));
                if (npc is null) { npc = new ImportantNpc { Name = member.Name }; state.ImportantCharacters.Add(npc); }
                npc.Category = "important"; npc.Attitude = string.IsNullOrWhiteSpace(npc.Attitude) ? L("дружелюбное", "friendly") : npc.Attitude;
                npc.Role = string.IsNullOrWhiteSpace(npc.Role) ? L("бывший спутник", "former companion") : npc.Role;
                npc.Note = (L("Покинул(а) группу по взаимной договорённости. Можно попытаться пригласить снова. ", "Left the party by mutual agreement. Can be invited again. ") + member.Note).Trim();
            }
            _rpg.Save(chatId, state);
            return (true, L($"OK: «{name}» больше не в группе.", $"OK: \"{name}\" is no longer in the party.") + (departure == "normal" ? L(" Персонаж сохранён среди важных знакомых.", " The character is kept among the important acquaintances.") : ""));
        }
        var createdMember = member is null;
        if (member is null)
        {
            // Новый спутник — только через воронку: так игрок выбирает роль, а лист собирается сбалансированным.
            var candidate = state.CompanionCandidates.FirstOrDefault(cc => cc.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return (false, L($"Ошибка: «{name}» нет в группе (сейчас: {(state.Party.Count > 0 ? string.Join(", ", state.Party.Select(p => p.Name)) : "никого")}). ", $"Error: \"{name}\" is not in the party (now: {(state.Party.Count > 0 ? string.Join(", ", state.Party.Select(p => p.Name)) : "nobody")}). ") +
                           (candidate is null
                               ? L("Нового спутника добавляй через update_companion_candidate action=introduce → совместное испытание → ready → ask_companion_role → recruit_companion.", "Add a new companion via update_companion_candidate action=introduce → a shared trial → ready → ask_companion_role → recruit_companion.")
                               : L($"Кандидат на этапе «{candidate.Stage}»: продолжи воронку (shared_trial → ready → ask_companion_role → recruit_companion).", $"The candidate is at the \"{candidate.Stage}\" stage: continue the funnel (shared_trial → ready → ask_companion_role → recruit_companion).")));
        }

        if (member is null)
        {
            if (state.Party.Count >= 3) return (false, L("Ошибка: в группе уже три компаньона — это максимум.", "Error: the party already has three companions — that is the maximum."));
            member = new PartyMember { Name = name, ManaCurrent = 10, ManaMax = 10, Level = state.Character.Level };
            state.Party.Add(member);
        }
        if (Str(args, "role") is { Length: > 0 } roleArg) member.Role = Progression.Archetype(roleArg).Key;
        if (Arg(args, "race") is { } race) member.Race = race.Trim();
        if (Arg(args, "class") is { } cls) member.CharClass = cls.Trim();
        if (Arg(args, "gender") is { } gender) member.Gender = gender.Trim();
        if (Arg(args, "level") is { } level) member.Level = level.Trim();
        if (Arg(args, "note") is { } note) member.Note = note.Trim();
        if (Arg(args, "portrait") is { } portrait) member.Portrait = portrait.Trim();
        if (Arg(args, "skill") is { } skill && skill.Trim().Length > 0 && !member.Skills.Contains(skill.Trim(), StringComparer.OrdinalIgnoreCase)) member.Skills.Add(skill.Trim());
        foreach (var (key, set) in new (string, Action<int>)[]
        {
            ("hp_current", v => member.HpCurrent = Math.Max(0, v)), ("hp_max", v => member.HpMax = Math.Max(0, v)),
            ("mana_current", v => member.ManaCurrent = Math.Max(0, v)), ("mana_max", v => member.ManaMax = Math.Max(0, v)),
            ("shield", v => member.Shield = Math.Max(0, v)),
            ("str", v => member.Str = Math.Clamp(v, 1, 30)), ("dex", v => member.Dex = Math.Clamp(v, 1, 30)),
            ("con", v => member.Con = Math.Clamp(v, 1, 30)), ("int", v => member.Int = Math.Clamp(v, 1, 30)),
            ("wis", v => member.Wis = Math.Clamp(v, 1, 30)), ("cha", v => member.Cha = Math.Clamp(v, 1, 30)),
        })
        {
            var value = OptInt(args, key, int.MinValue); if (value != int.MinValue) set(value);
        }
        if (args["hp_current"] is not null)
        {
            if (member.HpCurrent > 0) CombatRules.Revive(member); else CombatRules.EnterDying(member);
        }
        // Изменение HP на дельту — так боевой журнал знает, сколько именно снято или добавлено.
        if (OptInt(args, "hp_delta", 0) is var hpDelta && hpDelta != 0)
        {
            if (hpDelta < 0 && member.Shield > 0)
            {
                var absorbed = Math.Min(member.Shield, -hpDelta);
                member.Shield -= absorbed;
                hpDelta += absorbed;
            }
            if (hpDelta < 0) CombatRules.DamageWhileDying(member);
            member.HpCurrent = Math.Clamp(member.HpCurrent + hpDelta, 0, Math.Max(member.HpMax, member.HpCurrent));
            if (member.HpCurrent > 0) CombatRules.Revive(member); else CombatRules.EnterDying(member);
        }
        if (Str(args, "add_status") is { } addStatus && addStatus.Trim().Length > 0)
        {
            CombatEffects.AddOrRefresh(member.Status, addStatus, Num(args, "rounds"), state.InCombat());
        }
        if (Str(args, "remove_status") is { } removeStatus)
        {
            CombatEffects.Remove(member.Status, removeStatus);
        }
        // Новый спутник без чисел — ХП и мана по архетипу и уровню, облик по расе и полу.
        if (createdMember)
        {
            var arch = Progression.ArchetypeOf(member);
            var lvl = Progression.ParseLevel(member.Level);
            if (member.HpMax <= 0) member.HpMax = member.HpCurrent = Progression.HpAtLevel(arch, DndStatNames.Modifier(member.Con), lvl);
            if (args["mana_max"] is null) member.ManaMax = member.ManaCurrent = Progression.ManaAtLevel(arch, DndStatNames.Modifier(member.Stat(arch.Priority[0])), lvl);
        }
        if (createdMember || args["portrait"] is not null)
        {
            member.Portrait = PortraitCatalog.ResolvePerson(member.Portrait, member.Race, member.Gender, member.CharClass, member.Name)?.Id ?? member.Portrait;
        }

        _rpg.Save(chatId, state);
        return (true, L($"OK: участник группы «{name}» обновлён ({state.Party.Count}/3).", $"OK: the party member \"{name}\" is updated ({state.Party.Count}/3).") +
                       (createdMember ? L(" Совет: новых спутников добавляй через update_companion_candidate → ask_companion_role → recruit_companion — со знакомством, согласием, снаряжением и навыками по уровню.", " Tip: add new companions via update_companion_candidate → ask_companion_role → recruit_companion — with an acquaintance, consent, gear and skills by level.") : ""));
    }

    private (bool Ok, string Result) UpdateSkill(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var owner = (Arg(args, "owner") ?? "").Trim(); var name = (Arg(args, "name") ?? "").Trim();
        if (owner.Length == 0 || name.Length == 0) return (false, L("Ошибка: нужны owner и name.", "Error: owner and name are required."));
        var state = _rpg.GetOrCreate(chatId);
        List<SkillRecord>? skills = owner.Equals("hero", StringComparison.OrdinalIgnoreCase) ? state.Character.Skills : state.Party.FirstOrDefault(p => p.Name.Equals(owner, StringComparison.OrdinalIgnoreCase))?.LearnedSkills;
        if (skills is null) return (false, L($"Ошибка: владелец навыка «{owner}» не найден.", $"Error: the skill owner \"{owner}\" was not found."));
        var skill = skills.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (OptBool(args, "remove")) { if (skill is null) return (false, L("Ошибка: навык не найден.", "Error: the skill was not found.")); skills.Remove(skill); _rpg.Save(chatId, state); return (true, L($"OK: {owner} забыл(а) навык «{name}». Свободно слотов: {8-skills.Count}.", $"OK: {owner} forgot the skill \"{name}\". Free slots: {8-skills.Count}.")); }
        var isNew = skill is null;
        if (skill is null) { if (skills.Count >= 8) return (false, L("Ошибка: все 8 слотов навыков заняты. Игрок должен выбрать навык, который будет забыт; затем вызови update_skill с remove=true.", "Error: all 8 skill slots are taken. The player must choose a skill to forget; then call update_skill with remove=true.")); skill = new SkillRecord { Name = name }; skills.Add(skill); }
        var ownerLevel = Progression.ParseLevel(owner.Equals("hero", StringComparison.OrdinalIgnoreCase)
            ? state.Character.Level
            : state.Party.FirstOrDefault(p => p.Name.Equals(owner, StringComparison.OrdinalIgnoreCase))?.Level);
        var skillNotes = new List<string>();
        if (Arg(args, "category") is { } cat) skill.Category = cat.Trim().ToLowerInvariant();
        if (args["rank"] is not null)
        {
            var wanted = Math.Clamp(Num(args, "rank") ?? skill.Rank, 1, 5);
            var cap = Progression.MaxSkillRank(ownerLevel);
            if (wanted > cap && !Flag(args, "beyond_level"))
            {
                skillNotes.Add(L($"ступень {wanted} недоступна на уровне {ownerLevel} — записана {cap} (следующая откроется с ростом уровня)", $"tier {wanted} is unavailable at level {ownerLevel} — recorded {cap} (the next one opens as the level grows)"));
                wanted = cap;
            }

            skill.Rank = wanted;
        }
        if (args["mana_cost"] is not null) skill.ManaCost = Math.Max(0, Num(args, "mana_cost") ?? skill.ManaCost);
        else if (isNew && skill.ManaCost == 0 && skill.Category is not "utility") skill.ManaCost = new[] { 3, 5, 9, 15, 24 }[skill.Rank - 1];
        if (Arg(args, "target") is { } target) skill.Target = target.Trim();
        if (Arg(args, "description") is { } desc) skill.Description = desc.Trim();
        if (Arg(args, "icon") is { } icon) skill.Icon = icon.Trim();
        if (Arg(args, "evolution") is { } evolution) skill.Evolution = evolution.Trim();
        _rpg.Save(chatId, state);
        var shownIcon = SkillIcons.Resolve(skill.Icon, skill.Name, skill.Description, skill.Category);
        return (true, L($"OK: навык «{skill.Name}» владельца {owner}, ступень {skill.Rank}, {skill.ManaCost} маны, иконка {shownIcon.Id} ({SkillIcons.TitleOf(shownIcon)}); занято {skills.Count}/8.", $"OK: the skill \"{skill.Name}\" of {owner}, tier {skill.Rank}, {skill.ManaCost} mana, icon {shownIcon.Id} ({SkillIcons.TitleOf(shownIcon)}); used {skills.Count}/8.") +
                      (Progression.ExplainPowerDice(skill.Description, ownerLevel) is var expl && expl != skill.Description ? L(" Сейчас: ", " Now: ") + expl + "." : "") +
                      (skillNotes.Count > 0 ? " " + string.Join("; ", skillNotes) + "." : ""));
    }

    private (bool Ok, string Result) UpdatePartyInventory(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        if (!OptBool(args, "consent")) return (false, L("Ошибка: участник не дал явного согласия на передачу вещи.", "Error: the member has not given explicit consent to the transfer."));
        var state = _rpg.GetOrCreate(chatId);
        var memberName = (Arg(args, "member") ?? "").Trim();
        var itemName = (Arg(args, "item") ?? "").Trim();
        var member = state.Party.FirstOrDefault(p => p.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase));
        if (member is null || itemName.Length == 0) return (false, L("Ошибка: участник или предмет не найден.", "Error: the member or the item was not found."));
        var item = member.Grid.FirstOrDefault(i => i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));
        var qty = Math.Max(0, OptInt(args, "quantity", 0));
        if (qty == 0)
        {
            if (item is not null) member.Grid.Remove(item);
        }
        else
        {
            if (item is null) { var def=_catalog.Resolve(Arg(args,"icon"),itemName); item=new GridItem{Name=itemName,Icon=def.Id,W=def.W,H=def.H}; var spot=InventoryOps.FindFree(member.Grid,item.W,item.H); if(spot is null)return(false,L("Ошибка: в инвентаре спутника нет места.", "Error: there is no room in the companion's inventory.")); (item.Col,item.Row)=spot.Value; member.Grid.Add(item); }
            item.Quantity = qty;
            if (Arg(args, "note") is { } note) item.Note = note.Trim();
        }
        _rpg.Save(chatId, state);
        return (true, L($"OK: инвентарь «{member.Name}» обновлён с его/её согласия.", $"OK: the inventory of \"{member.Name}\" is updated with their consent."));
    }

    private (bool Ok, string Result) TransferPartyItem(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        if (!OptBool(args, "consent")) return (false, L("Ошибка: спутник ещё не дал явного согласия на передачу.", "Error: the companion has not given explicit consent to the transfer yet."));
        var state = _rpg.GetOrCreate(chatId);
        var memberName = (Arg(args, "member") ?? "").Trim();
        var member = state.Party.FirstOrDefault(p => p.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase));
        if (member is null) return (false, L($"Ошибка: спутник «{memberName}» не найден.", $"Error: the companion \"{memberName}\" was not found."));
        var direction = (Arg(args, "direction") ?? "").Trim().ToLowerInvariant();
        if (direction is not ("hero_to_party" or "party_to_hero")) return (false, L("Ошибка: direction должен быть hero_to_party или party_to_hero.", "Error: direction must be hero_to_party or party_to_hero."));
        var source = direction == "hero_to_party" ? state.Grid : member.Grid;
        var destination = direction == "hero_to_party" ? member.Grid : state.Grid;
        var id = (Arg(args, "item_id") ?? "").Trim();
        var name = (Arg(args, "item") ?? "").Trim();
        var item = source.FirstOrDefault(i => id.Length > 0 && i.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                   ?? source.FirstOrDefault(i => name.Length > 0 && i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (item is null) return (false, L("Ошибка: предмет не найден у владельца. Не создавай его заново.", "Error: the item was not found with the owner. Do not create it anew."));
        var quantity = Math.Clamp(OptInt(args, "quantity", 1), 1, Math.Max(1, item.Quantity));
        var copy = InventoryOps.CloneForTransfer(item, quantity);
        var stack = destination.FirstOrDefault(i => InventoryOps.CanStack(i, copy));
        if (stack is null)
        {
            var spot = InventoryOps.FindFree(destination, copy.W, copy.H);
            if (spot is null) return (false, L($"Ошибка: в инвентаре получателя нет места {copy.W}×{copy.H} для «{copy.Name}». Передача отменена, предмет остался у владельца.", $"Error: the recipient's inventory has no {copy.W}×{copy.H} room for \"{copy.Name}\". The transfer is cancelled, the item stays with the owner."));
            (copy.Col, copy.Row) = spot.Value;
            destination.Add(copy);
        }
        else
        {
            stack.Quantity += quantity;
        }
        if (quantity >= item.Quantity) source.Remove(item); else item.Quantity -= quantity;
        _rpg.Save(chatId, state);
        var from = direction == "hero_to_party" ? L("героя", "the hero") : member.Name;
        var to = direction == "hero_to_party" ? member.Name : L("героя", "the hero");
        return (true, L($"OK: «{item.Name}» ×{quantity} передан из инвентаря {from} в инвентарь {to}; дублирования нет.", $"OK: \"{item.Name}\" ×{quantity} moved from the inventory of {from} to the inventory of {to}; no duplication."));
    }

    private (bool Ok, string Result) ConsumeItem(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var state = _rpg.GetOrCreate(chatId);
        var owner = (Arg(args, "owner") ?? "hero").Trim();
        List<GridItem>? bag = owner.Equals("hero", StringComparison.OrdinalIgnoreCase)
            ? state.Grid
            : state.Party.FirstOrDefault(p => p.Name.Equals(owner, StringComparison.OrdinalIgnoreCase))?.Grid;
        if (bag is null) return (false, L($"Ошибка: владелец «{owner}» не найден.", $"Error: the owner \"{owner}\" was not found."));
        if (!owner.Equals("hero", StringComparison.OrdinalIgnoreCase) && !OptBool(args, "consent"))
            return (false, L("Ошибка: нельзя расходовать вещь спутника без его согласия.", "Error: a companion's item cannot be consumed without their consent."));
        var id = (Arg(args, "item_id") ?? "").Trim();
        var name = (Arg(args, "item") ?? "").Trim();
        var item = bag.FirstOrDefault(i => id.Length > 0 && i.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                   ?? bag.FirstOrDefault(i => name.Length > 0 && i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (item is null) return (false, L("Ошибка: расходник не найден у указанного владельца.", "Error: the consumable was not found with the specified owner."));
        if (!IsConsumable(item)) return (false, L($"Ошибка: «{item.Name}» не помечен как расходник.", $"Error: \"{item.Name}\" is not marked as a consumable."));
        var quantity = Math.Clamp(OptInt(args, "quantity", 1), 1, Math.Max(1, item.Quantity));
        if (quantity >= item.Quantity) bag.Remove(item); else item.Quantity -= quantity;
        _rpg.Save(chatId, state);
        return (true, L($"OK: израсходовано «{item.Name}» ×{quantity} из инвентаря {(owner.Equals("hero", StringComparison.OrdinalIgnoreCase) ? "героя" : owner)}. Теперь отдельно зафиксируй эффект на выбранной цели.", $"OK: consumed \"{item.Name}\" ×{quantity} from the inventory of {(owner.Equals("hero", StringComparison.OrdinalIgnoreCase) ? "the hero" : owner)}. Now record the effect on the chosen target separately."));
    }

    private bool IsConsumable(GridItem item)
    {
        if (item.Consumable) return true;
        var category = _catalog.Get(item.Icon)?.Cat;
        return category is "potion" or "food" or "scroll" or "book";
    }

    private (bool Ok, string Result) UpdateWorldEvent(string? chatId, JsonObject args)
    {
        if (chatId is null)
        {
            return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        }

        var name = (Arg(args, "name") ?? "").Trim();
        if (name.Length == 0)
        {
            return (false, L("Ошибка: не указано название события (name).", "Error: the event name (name) is not given."));
        }

        var state = _rpg.GetOrCreate(chatId);
        var ev = state.WorldEvents.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var created = ev is null;
        if (ev is null)
        {
            ev = new WorldEventRecord { Name = name, State = "ongoing" };
            state.WorldEvents.Add(ev);
        }

        if (Arg(args, "description") is { } d && d.Trim().Length > 0)
        {
            ev.Description = d.Trim();
        }

        if (Arg(args, "impact") is { } i && i.Trim().Length > 0)
        {
            ev.Impact = i.Trim();
        }

        var st = (Arg(args, "state") ?? "").Trim().ToLowerInvariant();
        if (st is "ongoing" or "upcoming" or "ended")
        {
            ev.State = st;
        }

        _rpg.Save(chatId, state);
        return (true, L($"OK: событие «{ev.Name}» {(created ? "добавлено" : "обновлено")} [{ev.State}].", $"OK: the event \"{ev.Name}\" is {(created ? "added" : "updated")} [{ev.State}]."));
    }

    private (bool Ok, string Result) SetWorldState(string? chatId, JsonObject args)
    {
        if (chatId is null)
        {
            return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        }

        var text = (Str(args, "text") ?? "").Trim();
        var dayPart = ParseDayPart(Str(args, "day_part"));
        var daysPassed = Math.Clamp(Num(args, "days_passed") ?? 0, 0, 365);
        if (text.Length == 0 && dayPart is null && daysPassed == 0)
        {
            return (false, L("Ошибка: пустой текст состояния мира (text). Чтобы только сдвинуть время — передай day_part или days_passed.", "Error: empty world state text (text). To only move the time — pass day_part or days_passed."));
        }

        var state = _rpg.GetOrCreate(chatId);
        if (text.Length > 0) state.WorldState = text;

        // Место сцены может смениться и без travel (герой просто ушёл в лес, вернулся из данжа):
        // прежние nearby привязаны к своему месту и до возвращения из «неподалёку» уйдут.
        var place = (Str(args, "place") ?? "").Trim();
        var moved = place.Length > 0 && !string.Equals(state.ScenePlace ?? "", place, StringComparison.OrdinalIgnoreCase);
        if (moved)
        {
            // Как в travel: соседи без места остаются там, где их встретили, — иначе они пропали бы навсегда.
            if (!string.IsNullOrWhiteSpace(state.ScenePlace))
            {
                foreach (var npc in state.ImportantCharacters.Where(n => n.Category == "nearby" && string.IsNullOrWhiteSpace(n.NearbyPlace)))
                {
                    npc.NearbyPlace = state.ScenePlace;
                }
            }

            state.ScenePlace = place;
        }

        // Время: дни и часть суток. Более ранняя часть суток без days_passed — значит, наступил следующий день.
        var news = new List<string>();
        if (dayPart is not null || daysPassed > 0)
        {
            var parts = dayPart is int dp ? (dp - state.DayPart + 4) % 4 : 0;
            if (dayPart is int same && same == state.DayPart && daysPassed == 0) parts = 0;
            news = AdvanceTime(state, daysPassed, parts);
        }

        _rpg.Save(chatId, state);
        var timeText = dayPart is not null || daysPassed > 0 ? L(" Время: ", " Time: ") + TimeText(state) + "." : "";
        return (true, (moved
            ? L($"OK: состояние мира обновлено; место сцены — «{place}». Прежние торговцы и жители остались в своём месте и скрыты из «Взаимодействия неподалёку» до возвращения.", $"OK: the world state is updated; the scene place is \"{place}\". The previous merchants and residents stayed in their place and are hidden from \"Nearby interactions\" until the hero returns.")
            : L("OK: состояние мира обновлено.", "OK: the world state is updated.")) + timeText + (news.Count > 0 ? "\n" + string.Join("\n", news) : ""));
    }

    // ===== Кости и выбор игрока =====

    private static readonly Regex DiceTerm = new(
        @"([+-])?\s*(?:(\d*)\s*[dдк]\s*(\d+|%)\s*(?:k([hl])\s*(\d+))?|(\d+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Итог броска — хвост «= число» в результате roll_dice.</summary>
    private static readonly Regex DiceTotalRe = new(@"=\s*(-?\d+)\s*$", RegexOptions.Compiled);

    /// <summary>Слово «инициатив» в причине броска — как в журнале боя, чтобы оба источника совпадали.</summary>
    private static readonly Regex InitiativeWordRe = new(@"инициатив|initiative", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Бросок костей. Инициатива из такого броска дополнительно пишется в состояние кампании:
    /// очередь хода должна переживать обрезку истории и перезапуск приложения — иначе модель
    /// забывает порядок и пропускает ходы части участников.
    /// </summary>
    private (bool Ok, string Result) RollAndRecord(string? chatId, JsonObject args)
    {
        var (ok, text) = RollDice(Str(args, "expression"), Str(args, "reason"));
        if (!ok)
        {
            return (ok, text);
        }

        var reason = Str(args, "reason") ?? "";
        var check = (Str(args, "check") ?? "").Trim().ToLowerInvariant();
        if (chatId is null || (check != "initiative" && !InitiativeWordRe.IsMatch(reason)))
        {
            return (ok, text);
        }

        var totalMatch = DiceTotalRe.Match(text);
        if (!totalMatch.Success || !int.TryParse(totalMatch.Groups[1].Value, out var total))
        {
            return (ok, text);
        }

        var who = (Str(args, "attacker") ?? "").Trim();
        if (who.Length == 0)
        {
            return (true, text +
                L("\n⚠ Инициатива не попала в очередь хода: не указан attacker (кто бросает). " +
                "Повтори бросок с attacker либо впиши число через initiative (name, value) — иначе порядок хода не сохранится.",
                "\n⚠ The initiative did not get into the turn order: attacker (who rolls) is not given. " +
                "Repeat the roll with attacker or enter the number via initiative (name, value) — otherwise the turn order will not be saved."));
        }

        var state = _rpg.GetOrCreate(chatId);
        var stale = state.Initiative.FindIndex(s => s.Name.Equals(who, StringComparison.OrdinalIgnoreCase));
        if (stale >= 0)
        {
            // Переброс: новое число, позиция в очереди — по времени этого броска (для равных инициатив).
            state.Initiative.RemoveAt(stale);
        }

        state.Initiative.Add(new InitiativeSeat { Name = who, Total = total });
        _rpg.Save(chatId, state);

        return (true, text + L("\nОчередь хода: ", "\nTurn order: ") + Initiative.Line(state.Initiative) + "." + MissingNote(state));
    }

    /// <summary>Кто из живых участников боя стоит без места в очереди (пусто — все на месте).</summary>
    private static string MissingNote(RpgState state)
    {
        var missing = Initiative.Missing(state);
        return missing.Count == 0
            ? ""
            : L($"\nБез места в очереди: {string.Join(", ", missing)} — брось их инициативу (roll_dice, check=initiative) " +
              "или впиши через initiative (name, value), иначе их ходы потеряются.",
              $"\nWithout a place in the queue: {string.Join(", ", missing)} — roll their initiative (roll_dice, check=initiative) " +
              "or enter it via initiative (name, value), otherwise their turns will be lost.");
    }

    /// <summary>
    /// Очередь хода: чтение, запись участника, удаление и сброс. Числа пишет харнес, модель их только
    /// запрашивает и поправляет — поэтому порядок одинаков в сводке состояния, в ответе инструмента
    /// и в бейджах окна боя.
    /// </summary>
    private (bool Ok, string Result) InitiativeTool(string? chatId, JsonObject args)
    {
        if (chatId is null)
        {
            return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        }

        var state = _rpg.GetOrCreate(chatId);
        var name = (Str(args, "name") ?? "").Trim();

        if (Flag(args, "clear"))
        {
            state.Initiative.Clear();
            state.CombatRound = 0;
            state.CombatCurrentActor = "";
            _rpg.Save(chatId, state);
            return (true, L("OK: очередь хода сброшена, инициатива не брошена. " +
                "Брось её заново для всех участников (roll_dice, check=initiative, attacker=<имя>) либо набери числа через initiative (name, value).",
                "OK: the turn order is reset, initiative is not rolled. " +
                "Roll it again for all participants (initiative roll_all=true) or enter the numbers via initiative (name, value)."));
        }

        if (Flag(args, "remove"))
        {
            if (name.Length == 0)
            {
                return (false, L("Ошибка: для remove нужен name — кого убрать из очереди.", "Error: remove needs name — whom to remove from the queue."));
            }

            var at = state.Initiative.FindIndex(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (at < 0)
            {
                return (false, L($"Ошибка: «{name}» не стоит в очереди хода.", $"Error: \"{name}\" is not in the turn order."));
            }

            state.Initiative.RemoveAt(at);
            _rpg.Save(chatId, state);
            return (true, L($"OK: {name} убран из очереди.\nОчередь хода:\n", $"OK: {name} is removed from the queue.\nTurn order:\n") + Initiative.Numbered(state.Initiative) + MissingNote(state));
        }

        if (Flag(args, "roll_all"))
        {
            return RollAllInitiative(chatId, state, (Str(args, "ambush") ?? "").Trim().ToLowerInvariant());
        }

        if (Num(args, "value") is { } value)
        {
            if (name.Length == 0)
            {
                return (false, L("Ошибка: для записи нужен name — кто бросает инициативу.", "Error: name is needed to record — who rolls initiative."));
            }

            value = Math.Clamp(value, 0, 100);
            var known = state.Initiative.FindIndex(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (known >= 0)
            {
                state.Initiative[known].Total = value;
            }
            else
            {
                state.Initiative.Add(new InitiativeSeat { Name = name, Total = value });
            }

            _rpg.Save(chatId, state);
            Initiative.Order(state.Initiative).TryGetValue(name, out var seat);
            return (true, L($"OK: {name} — инициатива {value}, место в очереди {seat}.\nОчередь хода:\n", $"OK: {name} — initiative {value}, place in the queue {seat}.\nTurn order:\n") + Initiative.Numbered(state.Initiative) + MissingNote(state));
        }

        if (name.Length > 0)
        {
            if (Initiative.Order(state.Initiative).TryGetValue(name, out var seat))
            {
                return (true, L($"{name} — место {seat} из {state.Initiative.Count}.\nОчередь хода:\n", $"{name} — place {seat} of {state.Initiative.Count}.\nTurn order:\n") + Initiative.Numbered(state.Initiative));
            }

            return (true, L($"{name} не стоит в очереди хода. Брось его инициативу (roll_dice, check=initiative, attacker=\"{name}\") " +
                          $"или впиши через initiative (name, value).\nОчередь хода:\n",
                          $"{name} is not in the turn order. Roll their initiative (roll_dice, check=initiative, attacker=\"{name}\") " +
                          $"or enter it via initiative (name, value).\nTurn order:\n") + Initiative.Numbered(state.Initiative));
        }

        if (state.Initiative.Count == 0)
        {
            return (true, L("Очередь хода пуста — инициатива не брошена. В начале боя брось d20 + мод ЛОВ для каждого участника " +
                          "через roll_dice (check=initiative, attacker=<имя>): числа сами встанут в очередь и останутся в ней между ходами.",
                          "The turn order is empty — initiative is not rolled. At the start of combat call initiative roll_all=true " +
                          "(or roll d20 + DEX mod for each participant via roll_dice check=initiative, attacker=<name>): the numbers enter the queue and stay there between turns."));
        }

        return (true, L("Очередь хода (сверху — ходит первым):\n", "Turn order (the top one acts first):\n") + Initiative.Numbered(state.Initiative) + "\n" +
                      L("Веди раунд строго по этим местам: у каждого участника свой номер, всех надо провести за раунд.", "Run the round strictly by these places: each participant has their own number, everyone must act each round.") + MissingNote(state));
    }

    /// <summary>
    /// Инициатива всем, кто ещё без места: герой и спутники — d20 + ЛОВ с вещами, противники — d20 + бонус по архетипу и роли.
    /// Засада даёт преимущество устроившей её стороне. Если очередь полна и раунд не начат — сразу начинает раунд 1.
    /// </summary>
    private (bool Ok, string Result) RollAllInitiative(string chatId, RpgState state, string ambush)
    {
        if (!state.InCombat())
        {
            return (false, L("Ошибка: в бою нет противников. Сначала выведи их через plan_encounter.", "Error: there are no adversaries in combat. Bring them in via plan_encounter first."));
        }

        var rolled = new List<string>();
        var events = new List<CombatEvent>();
        void Roll(string name, int bonus, bool advantage)
        {
            if (Initiative.HasSeat(state.Initiative, name, state.Character.Name)) return;
            var first = Random.Shared.Next(1, 21);
            var face = advantage ? Math.Max(first, Random.Shared.Next(1, 21)) : first;
            var total = face + bonus;
            state.Initiative.Add(new InitiativeSeat { Name = name, Total = total });
            rolled.Add($"{name} d20 {face}{(bonus != 0 ? $" {(bonus > 0 ? "+" : "−")} {Math.Abs(bonus)}" : "")} = {total}{(advantage ? L(" (засада)", " (ambush)") : "")}");
            events.Add(new CombatEvent("initiative", "⚑", name, null, null, total, total.ToString(), null, null, false, Label: L("Инициатива", "Initiative"), Text: L("инициатива", "initiative")));
        }

        var heroesAmbush = ambush is "heroes" or "hero" or "party" or "players" or "герой" or "группа";
        var foesAmbush = ambush is "enemies" or "foes" or "враги" or "противники";
        var hero = state.Character;
        Roll(hero.Name.Length > 0 ? hero.Name : Initiative.HeroAlias, DndStatNames.Modifier(ItemStats.Effective(hero, DndStat.Dex)), heroesAmbush);
        foreach (var m in state.Party.Where(p => p.HpCurrent > 0))
        {
            Roll(m.Name, DndStatNames.Modifier(ItemStats.Effective(m, DndStat.Dex)), heroesAmbush);
        }

        foreach (var foe in state.Adversaries.Where(a => a.InFight))
        {
            Roll(foe.Name, CombatRules.FoeInitiativeBonus(foe), foesAmbush);
        }

        var sb = new StringBuilder();
        sb.Append(rolled.Count > 0 ? L("OK: инициатива брошена — ", "OK: initiative rolled — ") + string.Join("; ", rolled) + ".\n" : L("OK: у всех уже есть место в очереди.\n", "OK: everyone already has a place in the queue.\n"));
        sb.Append(L("Очередь хода: ", "Turn order: ") + Initiative.Line(state.Initiative) + ".");
        if (state.CombatRound <= 0 && Initiative.Missing(state).Count == 0 && CombatRules.ActiveSeats(state) is { Count: > 0 } seats)
        {
            state.CombatRound = 1;
            state.CombatCurrentActor = seats[0];
            var notes = CombatRules.StartTurn(state, seats[0], events);
            sb.Append(L($"\nРаунд 1 начат. Сейчас ходит: {seats[0]}.", $"\nRound 1 has started. Now acting: {seats[0]}."));
            if (notes.Count > 0) sb.Append("\n" + string.Join("\n", notes));
            if (!CombatRules.SameActor(state, seats[0], hero.Name))
            {
                sb.Append(L(" Проведи всех, кто стоит перед героем (resolve_attack, затем combat_turn advance), и остановись перед ходом героя.", " Run everyone who stands before the hero (resolve_attack, then combat_turn advance), and stop before the hero's turn."));
            }

            if (foesAmbush)
            {
                sb.Append(L(" Засада врагов: в первом раунде застигнутые врасплох герои не реагируют (без реакций).", " Enemy ambush: in the first round the surprised heroes do not react (no reactions)."));
            }
        }

        _rpg.Save(chatId, state);
        CombatEvents.Push(chatId, events);
        return (true, sb.ToString());
    }

    /// <summary>
    /// Честный бросок: «1d20+3», «2d6+1d4-1», «4d6kh3», «d100», «1d20 adv» / «1d20 dis».
    /// </summary>
    internal static (bool Ok, string Result) RollDice(string? expression, string? reason)
    {
        var expr = (expression ?? "").Trim();
        if (expr.Length == 0)
        {
            return (false, L("Ошибка: пустая формула броска (expression).", "Error: empty roll formula (expression)."));
        }

        var lower = expr.ToLowerInvariant();
        var adv = lower.Contains("adv") || lower.Contains("преим");
        var dis = lower.Contains("dis") || lower.Contains("помех");
        var core = Regex.Replace(lower, @"\b(adv|advantage|dis|disadvantage)\b|преимуществ\w*|помех\w*", "").Trim();

        var parts = new List<string>();
        var total = 0;
        var dicePool = 0;
        var matched = 0;
        foreach (Match m in DiceTerm.Matches(core))
        {
            if (m.Length == 0 || m.Value.Trim().Length == 0)
            {
                continue;
            }

            matched += m.Value.Trim().Length;
            var sign = m.Groups[1].Value == "-" ? -1 : 1;
            var signText = parts.Count == 0 ? (sign < 0 ? "−" : "") : sign < 0 ? " − " : " + ";

            if (m.Groups[6].Success)
            {
                var flat = int.Parse(m.Groups[6].Value);
                total += sign * flat;
                parts.Add(signText + flat);
                continue;
            }

            var count = m.Groups[2].Value.Length == 0 ? 1 : int.Parse(m.Groups[2].Value);
            var sides = m.Groups[3].Value == "%" ? 100 : int.Parse(m.Groups[3].Value);
            if (count is < 1 or > 100 || sides is < 2 or > 1000 || (dicePool += count) > 200)
            {
                return (false, L("Ошибка: слишком много костей или граней (до 100 костей, до 1000 граней).", "Error: too many dice or faces (up to 100 dice, up to 1000 faces)."));
            }

            var rolls = Enumerable.Range(0, count).Select(_ => Random.Shared.Next(1, sides + 1)).ToList();
            var note = "";

            // Преимущество / помеха: первая одиночная d20 бросается дважды.
            if ((adv || dis) && count == 1 && sides == 20)
            {
                var second = Random.Shared.Next(1, 21);
                var first = rolls[0];
                rolls[0] = adv ? Math.Max(first, second) : Math.Min(first, second);
                note = $"{first}/{second}→";
                adv = dis = false;
            }

            var kept = rolls;
            if (m.Groups[4].Success)
            {
                var keep = Math.Clamp(int.Parse(m.Groups[5].Value), 1, count);
                kept = (m.Groups[4].Value.Equals("h", StringComparison.OrdinalIgnoreCase)
                    ? rolls.OrderByDescending(r => r)
                    : rolls.OrderBy(r => r)).Take(keep).ToList();
            }

            total += sign * kept.Sum();
            var shown = count == 1 ? $"{note}{rolls[0]}" : string.Join(", ", rolls);
            var keptText = m.Groups[4].Success ? $" ⇒ {string.Join("+", kept)}" : "";
            parts.Add($"{signText}{count}d{sides}[{shown}{keptText}]");
        }

        if (parts.Count == 0 || matched < core.Replace(" ", "").Length / 2)
        {
            return (false, L($"Ошибка: не понимаю формулу «{expr}». Примеры: 1d20+3, 2d6, 4d6kh3, d100, 1d20 adv.", $"Error: I don't understand the formula \"{expr}\". Examples: 1d20+3, 2d6, 4d6kh3, d100, 1d20 adv."));
        }

        var why = string.IsNullOrWhiteSpace(reason) ? "" : $" ({reason!.Trim()})";
        return (true, $"🎲 {expr}{why}: {string.Concat(parts)} = {total}");
    }

    /// <summary>Ключи, под которыми модели кладут текст варианта, если присылают объект вместо строки.</summary>
    private static readonly string[] ChoiceTextKeys = { "label", "text", "title", "option", "action", "name", "value", "description" };

    /// <summary>
    /// Текст варианта ответа. Модели иногда присылают не строку, а объект {"icon": "🌉", "label": "…"}
    /// (или такую строку-JSON) — берём текст и ставим иконку перед ним.
    /// </summary>
    public static string? ChoiceText(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var str))
        {
            var trimmed = str.Trim();
            if (!trimmed.StartsWith('{'))
            {
                return trimmed.Length > 0 ? trimmed : null;
            }

            try
            {
                node = JsonNode.Parse(trimmed);
            }
            catch
            {
                return trimmed;
            }
        }

        if (node is not JsonObject obj)
        {
            var raw = node?.ToString().Trim();
            return string.IsNullOrEmpty(raw) ? null : raw;
        }

        var text = ChoiceTextKeys
            .Select(k => TryString(obj[k])?.Trim())
            .FirstOrDefault(t => !string.IsNullOrEmpty(t));
        if (text is null)
        {
            return null;
        }

        var icon = (TryString(obj["icon"]) ?? TryString(obj["emoji"]))?.Trim();
        return string.IsNullOrEmpty(icon) || text.StartsWith(icon) ? text : $"{icon} {text}";
    }

    private static (bool Ok, string Result) OfferChoices(JsonObject args)
    {
        var options = (args["options"] as JsonArray)?
            .Select(ChoiceText)
            .Where(o => !string.IsNullOrEmpty(o))
            .ToList() ?? new();
        if (options.Count < 2)
        {
            return (false, L("Ошибка: нужно 2–6 вариантов в options (массив строк).", "Error: options needs 2–6 options (an array of strings)."));
        }

        return (true, L($"OK: игроку показаны кнопки ({options.Count}). Заверши ход и дождись его выбора или свободного ответа.", $"OK: buttons are shown to the player ({options.Count}). End the turn and wait for their choice or a free answer."));
    }

    private static (bool Ok, string Result) ProposeArcs(JsonObject args)
    {
        var arcs = (args["arcs"] as JsonArray)?.OfType<JsonObject>()
            .Where(a => !string.IsNullOrWhiteSpace(TryString(a["name"])))
            .ToList() ?? new();
        if (arcs.Count == 0)
        {
            return (false, L("Ошибка: передай массив arcs с объектами {name, premise}.", "Error: pass an arcs array of objects {name, premise}."));
        }

        return (true, L($"OK: игроку показаны карточки арок ({arcs.Count}). Заверши ход и дождись выбора; затем сохрани арку через update_arc со state=active.", $"OK: arc cards are shown to the player ({arcs.Count}). End the turn and wait for the choice; then save the arc via update_arc with state=active."));
    }

    private static string? TryString(JsonNode? node)
    {
        try
        {
            return node?.GetValue<string>();
        }
        catch
        {
            return node?.ToString();
        }
    }

    // ===== Экипировка и сумка =====

    private static bool TryParseSlot(string? text, out EquipSlot slot, out bool twoHanded)
    {
        twoHanded = false;
        slot = EquipSlot.Hand1;
        var s = (text ?? "").Trim().ToLowerInvariant().Replace(" ", "");
        if (s.Length == 0)
        {
            return false;
        }

        if (s is "twohanded" or "two_handed" or "двуручное" or "двуручник" or "оберуки")
        {
            twoHanded = true;
            return true;
        }

        var map = new Dictionary<string, EquipSlot>
        {
            ["helmet"] = EquipSlot.Helmet, ["head"] = EquipSlot.Helmet, ["шлем"] = EquipSlot.Helmet, ["голова"] = EquipSlot.Helmet,
            ["body"] = EquipSlot.Body, ["chest"] = EquipSlot.Body, ["armor"] = EquipSlot.Body, ["торс"] = EquipSlot.Body, ["тело"] = EquipSlot.Body,
            ["cloak"] = EquipSlot.Cloak, ["cape"] = EquipSlot.Cloak, ["плащ"] = EquipSlot.Cloak,
            ["gloves"] = EquipSlot.Gloves, ["hands"] = EquipSlot.Gloves, ["перчатки"] = EquipSlot.Gloves,
            ["boots"] = EquipSlot.Boots, ["feet"] = EquipSlot.Boots, ["ботинки"] = EquipSlot.Boots, ["сапоги"] = EquipSlot.Boots,
            ["amulet"] = EquipSlot.Amulet, ["neck"] = EquipSlot.Amulet, ["амулет"] = EquipSlot.Amulet,
            ["ring1"] = EquipSlot.Ring1, ["ring"] = EquipSlot.Ring1, ["кольцо1"] = EquipSlot.Ring1, ["кольцо"] = EquipSlot.Ring1,
            ["ring2"] = EquipSlot.Ring2, ["кольцо2"] = EquipSlot.Ring2,
            ["belt"] = EquipSlot.Belt, ["пояс"] = EquipSlot.Belt,
            ["hand1"] = EquipSlot.Hand1, ["mainhand"] = EquipSlot.Hand1, ["weapon"] = EquipSlot.Hand1, ["hand"] = EquipSlot.Hand1,
            ["рука1"] = EquipSlot.Hand1, ["рука"] = EquipSlot.Hand1,
            ["hand2"] = EquipSlot.Hand2, ["offhand"] = EquipSlot.Hand2, ["shield"] = EquipSlot.Hand2, ["рука2"] = EquipSlot.Hand2, ["щит"] = EquipSlot.Hand2,
        };
        return map.TryGetValue(s, out slot);
    }

    private (bool Ok, string Result) EquipItem(string? chatId, JsonObject args)
    {
        if (chatId is null)
        {
            return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        }

        if (!TryParseSlot(Arg(args, "slot"), out var slot, out var twoHandedSlot))
        {
            return (false, L("Ошибка: slot должен быть одним из: Helmet, Body, Cloak, Gloves, Boots, Belt, Amulet, Ring1, Ring2, Hand1, Hand2 или TwoHanded.", "Error: slot must be one of: Helmet, Body, Cloak, Gloves, Boots, Belt, Amulet, Ring1, Ring2, Hand1, Hand2 or TwoHanded."));
        }

        var state = _rpg.GetOrCreate(chatId);

        if (OptBool(args, "unequip"))
        {
            var ok = InventoryOps.Unequip(state, twoHandedSlot ? EquipSlot.Hand1 : slot, _catalog, OptBool(args, "drop"), out var msg);
            if (ok)
            {
                _rpg.Save(chatId, state);
            }

            return (ok, (ok ? "OK: " : L("Ошибка: ", "Error: ")) + msg);
        }

        var name = (Arg(args, "name") ?? "").Trim();
        if (name.Length == 0)
        {
            return (false, L("Ошибка: не указано название предмета (name).", "Error: the item name (name) is not given."));
        }

        if (Regex.IsMatch(name, @"(?i)(^|\s)(золото|золотые монеты|монеты|gold|coins?)(\s|$)"))
            return (false, L("Ошибка: золото не является предметом инвентаря. Измени только числовое поле gold через update_character.", "Error: gold is not an inventory item. Change only the numeric gold field via update_character."));

        var twoHanded = twoHandedSlot || OptBool(args, "two_handed");
        var iconArg = Arg(args, "icon");

        // Предмет из сумки переносится на героя; иначе — новый предмет прямо из мира.
        var item = state.Grid.FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            var def = _catalog.Resolve(iconArg, name);
            item = new GridItem
            {
                Name = name,
                Icon = def.Id,
                W = def.W,
                H = def.H,
                Slot = def.EquipSlot,
                TwoHanded = def.TwoHanded,
            };

            // Иконка угадана по названию и не подходит слоту — верим слоту, который назвала модель.
            if (!twoHanded && !InventoryOps.CanGoTo(item.Slot, false, slot))
            {
                item.Slot = slot;
                item.TwoHanded = false;
            }
        }
        else if (_catalog.Get(iconArg) is { } newIcon)
        {
            item.Icon = newIcon.Id;
        }

        if (Arg(args, "note") is { } note && note.Trim().Length > 0)
        {
            item.Note = note.Trim();
        }

        if (twoHanded)
        {
            item.TwoHanded = true;
            item.Slot = EquipSlot.Hand1;
        }

        item.Slot ??= slot;
        var (rarityNote, usedToken) = ApplyRarity(state, item, Arg(args, "rarity"));
        rarityNote += ApplyItemStats(item, args, Progression.ParseLevel(state.Character.Level));

        var equipped = InventoryOps.Equip(state, item, item.TwoHanded ? null : slot, _catalog, out var message);
        if (!equipped && usedToken)
        {
            state.LegendaryTokens++;
        }

        message += equipped ? rarityNote : "";
        if (equipped)
        {
            _rpg.Save(chatId, state);
        }

        return (equipped, (equipped ? "OK: " : L("Ошибка: ", "Error: ")) + message);
    }

    private (bool Ok, string Result) UpdateGridItem(string? chatId, JsonObject args)
    {
        if (chatId is null)
        {
            return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        }

        var name = (Arg(args, "name") ?? "").Trim();
        if (name.Length == 0)
        {
            return (false, L("Ошибка: не указано название предмета (name).", "Error: the item name (name) is not given."));
        }
        if (Regex.IsMatch(name, @"(?i)(^|\s)(золото|золотые монеты|монеты|gold|coins?)(\s|$)"))
            return (false, L("Ошибка: золото не является предметом инвентаря. Измени только числовое поле gold через update_character.", "Error: gold is not an inventory item. Change only the numeric gold field via update_character."));

        var state = _rpg.GetOrCreate(chatId);

        if (OptBool(args, "remove"))
        {
            var removed = state.Grid.RemoveAll(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                _rpg.Save(chatId, state);
                return (true, L($"OK: «{name}» убран из сумки.", $"OK: \"{name}\" is removed from the bag."));
            }

            return (false, L($"Ошибка: «{name}» не найден в сумке. Сейчас там: ", $"Error: \"{name}\" was not found in the bag. It now holds: ") +
                           (state.Grid.Count == 0 ? L("пусто.", "nothing.") : string.Join(", ", state.Grid.Select(i => i.Name)) + "."));
        }

        var item = state.Grid.FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var created = item is null;
        var (oldW, oldH, oldCol, oldRow) = item is null ? (1, 1, 0, 0) : (item.W, item.H, item.Col, item.Row);

        var iconArg = Arg(args, "icon");
        if (item is null)
        {
            var def = _catalog.Resolve(iconArg, name);
            item = new GridItem
            {
                Name = name,
                Icon = def.Id,
                W = def.W,
                H = def.H,
                Slot = def.EquipSlot,
                TwoHanded = def.TwoHanded,
            };
        }
        else if (_catalog.Get(iconArg) is { } newDef && newDef.Id != item.Icon)
        {
            // Новая иконка — новый размер по каталогу (если размер не передан явно).
            item.Icon = newDef.Id;
            item.W = newDef.W;
            item.H = newDef.H;
            item.Slot = newDef.EquipSlot;
            item.TwoHanded = newDef.TwoHanded;
        }

        if (TryParseSlot(Arg(args, "slot"), out var slotHint, out var slotTwoHanded))
        {
            item.Slot = slotTwoHanded ? EquipSlot.Hand1 : slotHint;
            item.TwoHanded |= slotTwoHanded;
        }

        if (OptBool(args, "two_handed"))
        {
            item.TwoHanded = true;
            item.Slot = EquipSlot.Hand1;
        }

        if (args["consumable"] is not null)
        {
            item.Consumable = OptBool(args, "consumable");
        }
        else if (_catalog.Get(item.Icon)?.Cat is "potion" or "food" or "scroll" or "book")
        {
            item.Consumable = true;
        }

        if (OptInt(args, "quantity", int.MinValue) is var qty && qty != int.MinValue)
        {
            if (qty <= 0)
            {
                if (!created)
                {
                    state.Grid.Remove(item);
                    _rpg.Save(chatId, state);
                }

                return (true, L($"OK: «{name}» закончился и убран из сумки.", $"OK: \"{name}\" ran out and is removed from the bag."));
            }

            item.Quantity = qty;
        }

        if (Arg(args, "note") is { } note && note.Trim().Length > 0)
        {
            item.Note = note.Trim();
        }

        var (rarityNote, usedToken) = ApplyRarity(state, item, Arg(args, "rarity"));
        var statsNote = ApplyItemStats(item, args, Progression.ParseLevel(state.Character.Level));

        item.W = Math.Clamp(OptInt(args, "w", item.W), 1, 4);
        item.H = Math.Clamp(OptInt(args, "h", item.H), 1, 4);

        int? pc = OptInt(args, "col", -1) is var ci && ci >= 0 ? ci : null;
        int? pr = OptInt(args, "row", -1) is var ri && ri >= 0 ? ri : null;

        // Существующий предмет остаётся на месте, если помещается там с новым размером.
        if (!created && pc is null && InventoryOps.Fits(state.Grid, item.Col, item.Row, item.W, item.H, item))
        {
            pc = item.Col;
            pr = item.Row;
        }

        var spot = InventoryOps.FindFree(state.Grid, item.W, item.H, item, pc, pr);
        if (spot is null)
        {
            if (!created)
            {
                (item.W, item.H, item.Col, item.Row) = (oldW, oldH, oldCol, oldRow);
            }

            if (usedToken)
            {
                state.LegendaryTokens++;
            }

            var free =RpgState.GridCols * RpgState.GridRows - state.Grid.Where(i => !ReferenceEquals(i, item)).Sum(i => i.W * i.H);
            return (false, L($"Ошибка: в сумке нет места под {item.W}x{item.H} для «{name}» (свободно клеток: {free}, но не одним куском). " +
                           "Скажи игроку, что сумка полна: пусть решит, что выбросить или оставить.",
                           $"Error: the bag has no {item.W}x{item.H} room for \"{name}\" ({free} free cells, but not in one piece). " +
                           "Tell the player the bag is full: let them decide what to throw away or leave."));
        }

        (item.Col, item.Row) = spot.Value;
        if (created)
        {
            state.Grid.Add(item);
        }

        _rpg.Save(chatId, state);
        var def2 = _catalog.Get(item.Icon);
        return (true, L($"OK: «{name}» {(created ? "добавлен в сумку" : "обновлён")} — иконка {item.Icon}", $"OK: \"{name}\" {(created ? "added to the bag" : "updated")} — icon {item.Icon}") +
                      $"{(def2 is null ? "" : $" ({def2.Name})")}, {item.W}x{item.H}, {L("клетка", "cell")} {item.Col},{item.Row}" +
                      (item.Quantity > 1 ? $", x{item.Quantity}" : "") +
                      (item.Slot is { } s ? L("; можно надеть (", "; can be worn (") + InventoryOps.SlotTitle(s) + (item.TwoHanded ? L(", двуручное", ", two-handed") : "") + ")" : "") + "." + rarityNote + statsNote);
    }

    private static string DamageParamHelp => L(
        "Урон оружия: кость и тип, например «1d8 рубящий», «2d6 огненный» (опционально; для оружия из каталога подставляется сам). «-» — убрать",
        "Weapon damage: the die and type, for example \"1d8 slashing\", \"2d6 fire\" (optional; filled in automatically for catalog weapons). \"-\" — remove");

    private static string ArmorParamHelp => L(
        "Броня к КБ: лёгкий доспех 1–2, средний 3–6, тяжёлый 6–8, щит 1–3, шлем/перчатки/сапоги/плащ 0–1 (опционально; для каталога подставляется сама)",
        "Armor to AC: light armor 1–2, medium 3–6, heavy 6–8, shield 1–3, helmet/gloves/boots/cloak 0–1 (optional; filled in automatically for the catalog)");

    private static string BonusesParamHelp => L(
        "Числовые бонусы через запятую: str/dex/con/int/wis/cha, attack, damage, ac, hp, mana — например «str+1, attack+1» или «hp+10». " +
        "Бюджет по редкости: обычный 0, необычный 1, редкий 2, эпический 4, легендарный 6 очков (+1 к характеристике/атаке/урону = 1, +1 ac = 2, +5 hp или маны = 1). " +
        "Штрафы (str-1) возвращают до 2 очков. Задаётся целиком — заменяет прежние бонусы; «-» — убрать все",
        "Numeric bonuses, comma-separated: str/dex/con/int/wis/cha, attack, damage, ac, hp, mana — for example \"str+1, attack+1\" or \"hp+10\". " +
        "Budget by rarity: common 0, uncommon 1, rare 2, epic 4, legendary 6 points (+1 to an ability/attack/damage = 1, +1 ac = 2, +5 hp or mana = 1). " +
        "Penalties (str-1) return up to 2 points. Set as a whole — replaces the previous bonuses; \"-\" — remove all");

    private static string EffectsParamHelp => L(
        "Особые свойства и умения через «;» — например «невидимость в темноте ночью; видит сквозь иллюзии» или «навык: вскрытие замков». " +
        "Лимит по редкости: обычный 0, необычный 1, редкий 1, эпический 2, легендарный 3. Задаётся целиком; «-» — убрать все",
        "Special properties and abilities separated by \";\" — for example \"invisible in darkness at night; sees through illusions\" or \"skill: lockpicking\". " +
        "Limit by rarity: common 0, uncommon 1, rare 1, epic 2, legendary 3. Set as a whole; \"-\" — remove all");

    /// <summary>
    /// Ставит предмету урон, броню, бонусы и свойства из аргументов, подставляет базу каталога
    /// и приводит всё к бюджету редкости. Возвращает пометку для результата инструмента.
    /// </summary>
    private static string ApplyItemStats(GridItem item, JsonObject args, int defaultLevel = 0)
    {
        var notes = new List<string>();

        // Уровень предмета: явный, иначе уровень героя для новой вещи — от него растёт бюджет бонусов.
        if (Num(args, "level") is { } itemLevel && itemLevel > 0)
        {
            item.Level = Math.Clamp(itemLevel, 1, 60);
        }
        else if (item.Level == 0 && defaultLevel > 0)
        {
            item.Level = defaultLevel;
        }

        if (FlexText(args, "damage", ", ") is { } dmg && dmg.Trim().Length > 0)
        {
            item.Damage = dmg.Trim() is "-" or "нет" or "none" ? "" : dmg.Trim();
        }

        if (FlexText(args, "armor", "") is { } rawArmor && int.TryParse(rawArmor.Trim().TrimStart('+'), out var armor) && armor >= 0)
        {
            item.Armor = armor;
        }

        if (FlexText(args, "bonuses", ", ") is { } rawBonuses && rawBonuses.Trim().Length > 0)
        {
            if (rawBonuses.Trim() is "-" or "нет" or "none")
            {
                item.Bonuses.Clear();
            }
            else
            {
                var (parsed, bad) = ItemStats.ParseBonuses(rawBonuses);
                item.Bonuses = parsed;
                if (bad.Count > 0)
                {
                    notes.Add(L($"не понял бонусы: {string.Join(", ", bad)} (ключи: {string.Join("/", ItemStats.Keys)}, формат «str+1»)", $"could not parse the bonuses: {string.Join(", ", bad)} (keys: {string.Join("/", ItemStats.Keys)}, format \"str+1\")"));
                }
            }
        }

        if (FlexText(args, "effects", "; ") is { } rawEffects && rawEffects.Trim().Length > 0)
        {
            item.Effects = rawEffects.Trim() is "-" or "нет" or "none" ? new() : ItemStats.ParseEffects(rawEffects);
        }

        ItemStats.ApplyBase(item);
        notes.AddRange(ItemStats.Balance(item));

        var text = ItemStats.Describe(item);
        var result = text.Length > 0 ? L(" Характеристики: ", " Stats: ") + text + "." : "";
        if (notes.Count > 0)
        {
            result += L(" БАЛАНС: ", " BALANCE: ") + string.Join("; ", notes) + L(". Описывай предмет по итоговым характеристикам.", ". Describe the item by its final stats.");
        }

        if (item.Slot is not null && ItemEconomy.RarityKey(item.Rarity) != "common" && item.Bonuses.Count == 0 && item.Effects.Count == 0)
        {
            result += L($" У {ItemEconomy.RarityTitle(item.Rarity).ToLowerInvariant()} вещи нет ни бонусов, ни свойств — задай bonuses/effects, " +
                      $"бюджет {ItemStats.Budget(item.Rarity, item.Level)} очк. (уровень предмета {Math.Max(1, item.Level)}).",
                      $" The {ItemEconomy.RarityTitle(item.Rarity).ToLowerInvariant()} item has neither bonuses nor properties — set bonuses/effects, " +
                      $"budget {ItemStats.Budget(item.Rarity, item.Level)} pts. (item level {Math.Max(1, item.Level)}).");
        }

        return result;
    }

    private static string RarityParamHelp => L(
        "Редкость: common / uncommon / rare / epic / legendary (опционально; по умолчанию common). " +
        "legendary — только при разрешении на легендарную добычу (выпала в колесе дороги travel или завершена сюжетная арка), иначе предмет станет эпическим.",
        "Rarity: common / uncommon / rare / epic / legendary (optional; default common). " +
        "legendary — only with a legendary loot permission (rolled on the travel road wheel or a story arc completed), otherwise the item becomes epic.");

    /// <summary>
    /// Ставит предмету редкость. Легендарная — только при наличии разрешения (LegendaryTokens), иначе понижается до эпической.
    /// Возвращает пометку для результата инструмента и признак, что разрешение израсходовано.
    /// </summary>
    private static (string Note, bool UsedToken) ApplyRarity(RpgState state, GridItem item, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ("", false);
        }

        var key = ItemEconomy.RarityKey(raw);
        if (key == ItemEconomy.RarityKey(item.Rarity))
        {
            return ("", false);
        }

        var note = "";
        var used = false;
        if (key == "legendary")
        {
            if (state.LegendaryTokens > 0)
            {
                state.LegendaryTokens--;
                used = true;
                note = L($" Легендарная добыча выдана (осталось разрешений: {state.LegendaryTokens}).", $" Legendary loot handed out (permissions left: {state.LegendaryTokens}).");
            }
            else
            {
                key = "epic";
                note = L(" ВНИМАНИЕ: разрешения на легендарную добычу нет — предмет записан как ЭПИЧЕСКИЙ. " +
                   "Легендарные вещи появляются только через колесо дороги (travel) или в финале сюжетной арки. " +
                   "Не называй эту вещь легендарной в повествовании.",
                   " WARNING: there is no legendary loot permission — the item is recorded as EPIC. " +
                   "Legendary items appear only via the road wheel (travel) or at a story arc finale. " +
                   "Do not call this item legendary in the narration.");
            }
        }

        item.Rarity = key;
        if (string.IsNullOrWhiteSpace(item.Note) || item.Note == ItemEconomy.RarityNote("common"))
        {
            item.Note = ItemEconomy.RarityNote(key);
        }

        return (note, used);
    }

    /// <summary>Колесо дороги: честный бросок случайного события в пути.</summary>
    private (bool Ok, string Result) Travel(string? chatId, JsonObject args)
    {
        var from = (Arg(args, "from") ?? "").Trim();
        var to = (Arg(args, "to") ?? "").Trim();
        var distance = TravelEncounters.NormalizeDistance(Arg(args, "distance"));
        if (distance.Length == 0)
        {
            return (false, L("Ошибка: distance должен быть одним из: short, medium, long, epic.", "Error: distance must be one of: short, medium, long, epic."));
        }

        if (distance == "short")
        {
            return (true, L("Короткий переход внутри поселения — колесо дороги не крутится. Опиши путь одной-двумя фразами.", "A short move inside the settlement — the road wheel does not spin. Describe the way in a sentence or two."));
        }

        var terrain = TravelEncounters.NormalizeTerrain(Arg(args, "terrain"));
        var state = chatId is null ? null : _rpg.GetOrCreate(chatId);

        // Переход в другое место — граница сцены: всё, что привязано к прежнему месту (и ещё
        // не привязано — старые сохранения), остаётся там и исчезает из «Взаимодействия
        // неподалёку» до возвращения героя. Внутренний переход (short) сюда не попадает.
        var sceneMoved = false;
        if (state is not null && to.Length > 0 && !string.Equals(state.ScenePlace ?? "", to, StringComparison.OrdinalIgnoreCase))
        {
            if (from.Length > 0)
            {
                foreach (var npc in state.ImportantCharacters.Where(n => n.Category == "nearby" && string.IsNullOrWhiteSpace(n.NearbyPlace)))
                {
                    npc.NearbyPlace = from;
                }
            }

            state.ScenePlace = to;
            _rpg.Save(chatId, state);
            sceneMoved = true;
        }

        var spins = TravelEncounters.Roll(distance, terrain, (IReadOnlyCollection<string>?)state?.RecentEncounters ?? Array.Empty<string>());

        var sb = new StringBuilder();
        sb.Append(TravelEncounters.BuildMeta(from, to, distance, spins)).Append('\n');
        sb.Append(L("Колесо дороги: ", "Road wheel: ") + $"{(from.Length > 0 ? from : "?")} → {(to.Length > 0 ? to : "?")}, {TravelEncounters.DistanceTitle(distance)}")
          .Append(terrain.Length > 0 ? L(", местность: ", ", terrain: ") + terrain : "").Append(".\n");
        if (sceneMoved)
        {
            sb.Append(L($"Смена места: {(from.Length > 0 ? from : "прежнее место")} → {to}. Кто остался прежним, из «Взаимодействия неподалёку» убран — по возвращении снова появится; новых nearby заводи уже для «{to}».\n",
                      $"Change of place: {(from.Length > 0 ? from : "the previous place")} → {to}. Those who stayed behind are removed from \"Nearby interactions\" — they will reappear on return; create new nearby NPCs for \"{to}\".\n"));
        }
        for (var i = 0; i < spins.Count; i++)
        {
            sb.Append(TravelEncounters.Describe(spins[i], i, spins.Count)).Append('\n');
        }

        var legendary = spins.Count(s => s.Loot == "legendary");
        if (state is not null)
        {
            // Дорога занимает время: окрестности — часть суток, между городами — дни, между странами — недели.
            var (days, parts) = distance switch
            {
                "medium" => (0, Random.Shared.Next(1, 3)),
                "long" => (Random.Shared.Next(2, 5), 0),
                _ => (Random.Shared.Next(7, 18), 0),
            };
            var timeNews = AdvanceTime(state, days, parts);
            sb.Append(L($"В пути: {(days > 0 ? $"{days} дн." : parts == 1 ? "полдня" : "почти день")}; прибытие — {TimeText(state)}.\n",
                      $"On the way: {(days > 0 ? $"{days} d." : parts == 1 ? "half a day" : "almost a day")}; arrival — {TimeText(state)}.\n"));
            foreach (var line in timeNews) sb.Append(line).Append('\n');

            state.LegendaryTokens += legendary;
            foreach (var s in spins.Where(s => s.Encounter is not null))
            {
                state.RecentEncounters.Add(s.Encounter!.Id);
            }

            if (state.RecentEncounters.Count > TravelEncounters.NoRepeatWindow)
            {
                state.RecentEncounters.RemoveRange(0, state.RecentEncounters.Count - TravelEncounters.NoRepeatWindow);
            }

            _rpg.Save(chatId, state);
        }

        if (legendary > 0)
        {
            sb.Append(L("Выпала ЛЕГЕНДАРНАЯ добыча: одну такую вещь можно выдать (update_grid_item/equip_item с rarity=legendary), " +
                      "но только если герой пройдёт испытание и одолеет стражей. Отступит — разрешение сохранится на будущее.\n",
                      "LEGENDARY loot was rolled: one such item may be handed out (update_grid_item/equip_item with rarity=legendary), " +
                      "but only if the hero passes the trial and defeats the guardians. If they retreat, the permission is kept for the future.\n"));
        }

        if (spins.Any(s => s.Event))
        {
            sb.Append(L("Разыграй событие в мире и тоне кампании (детали, имена, противников придумай сам, противников заведи через update_adversary). " +
                      "Заверши ход выбором через offer_choices — с вариантом отказаться/обойти/сбежать, если он есть.",
                      "Play out the event in the campaign's world and tone (invent the details, names and enemies yourself, bring the enemies in via plan_encounter). " +
                      "End the turn with a choice via offer_choices — with an option to refuse/go around/flee, if there is one."));
            if (spins.Count > 1)
            {
                sb.Append(L(" Этапы идут по порядку: сейчас разыграй первое событие, следующее — когда герой продолжит путь.", " The stages go in order: play out the first event now, the next one — when the hero continues the journey."));
            }
        }

        return (true, sb.ToString().TrimEnd());
    }

    /// <summary>Резолвит относительный путь внутрь рабочей директории; null — если путь пытается выйти наружу.</summary>
    private static string? ResolveSafe(string workDir, string? relPath)
    {
        if (string.IsNullOrWhiteSpace(relPath))
        {
            return null;
        }

        string combined;
        try
        {
            var normalized = relPath.Trim()
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            combined = Path.GetFullPath(Path.Combine(workDir, normalized));
        }
        catch
        {
            return null;
        }

        var root = Path.GetFullPath(workDir);
        var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var isInside = combined.StartsWith(rootWithSep, comparison)
                       || string.Equals(combined, root, comparison);
        if (!isInside)
        {
            return null;
        }

        // GetFullPath нейтрализует ../, но не раскрывает symlink/junction. Запрещаем
        // reparse points внутри корня, иначе модель могла бы выйти из папки кампании.
        var relative = Path.GetRelativePath(root, combined);
        var current = root;
        foreach (var part in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (part.Length == 0 || part == ".")
            {
                continue;
            }

            current = Path.Combine(current, part);
            if (!File.Exists(current) && !Directory.Exists(current))
            {
                break;
            }

            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        return combined;
    }

    private static string Rel(string workDir, string fullPath)
    {
        var root = Path.GetFullPath(workDir);
        if (!root.EndsWith(Path.DirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

        var rel = fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? fullPath[root.Length..]
            : fullPath;
        return rel.Length == 0 ? "." : rel;
    }

    private static string? Arg(JsonObject o, string key) => o[key]?.GetValue<string>();

    /// <summary>
    /// Текст аргумента, даже если модель прислала не строку: число → «2», массив → элементы через sep,
    /// объект {"str":1} → «str+1, …».
    /// </summary>
    private static string? FlexText(JsonObject o, string key, string sep) => o[key] switch
    {
        null => null,
        JsonArray arr => string.Join(sep, arr.Select(x => x is JsonValue v && v.TryGetValue<string>(out var t) ? t : x?.ToJsonString())),
        JsonObject obj => string.Join(", ", obj.Select(kv => $"{kv.Key}{(kv.Value is JsonValue v && v.TryGetValue<int>(out var n) ? n.ToString("+0;-0") : kv.Value?.ToString())}")),
        JsonValue v when v.TryGetValue<string>(out var t) => t,
        var n => n.ToJsonString(),
    };

    private static bool OptBool(JsonObject o, string key) => o[key] is { } v && v.GetValue<bool>();

    private static int OptInt(JsonObject o, string key, int fallback) => o[key] is { } v ? v.GetValue<int>() : fallback;

    private static string ByteSize(long bytes) =>
        bytes < 1024 ? $"{bytes} {L("Б", "B")}" : $"{bytes / 1024.0:0.#} {L("КБ", "KB")}";

    private static bool LooksBinary(string text) =>
        text.AsSpan(0, Math.Min(text.Length, 512)).IndexOf('\0') >= 0;

    private static int CountOccurrences(string text, string find)
    {
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(find, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += find.Length;
        }

        return count;
    }

    private static string ReplaceFirst(string text, string find, string replace)
    {
        var i = text.IndexOf(find, StringComparison.Ordinal);
        return i < 0 ? text : text[..i] + replace + text[(i + find.Length)..];
    }
}
