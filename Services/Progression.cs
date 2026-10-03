namespace RPG_Harness.Services;

/// <summary>Боевой архетип персонажа (роль в группе): кость хитов, рост маны, ключевые характеристики.</summary>
public sealed record ClassArchetype(
    string Key, string Title, string Ask, string Emoji, int HitDie, int ManaBase, int ManaPerLevel,
    DndStat[] Priority, int BaseAc, string Weapon, string Armor, bool Spellcaster, string[] ClassIdeas);

/// <summary>
/// Прогрессия и баланс: уровни и опыт, рост героя и спутников, статблоки противников по уровню и роли,
/// оценка сложности встречи честной симуляцией. Все числа боя берутся отсюда — модель их не выдумывает.
/// Калибровка: стандартная группа из k героев уровня L против k рядовых врагов того же уровня — бой
/// ~3–4 раунда и 25–40% потерянного ХП группы на любом уровне 1–30 (см. docs/Progression.md).
/// </summary>
public static class Progression
{
    /// <summary>Опыт на один уровень (одинаковый на всех уровнях, как в PF2e). Средний бой даёт ~80.</summary>
    public const int XpPerLevel = 600;

    /// <summary>Свободные деньги нового героя сверх уже выданного стартового комплекта.</summary>
    public const int StartingMoney = 50;

    /// <summary>Уровень, после которого начинается «эндгейм» (уровни идут дальше, но медленнее растёт мощь).</summary>
    public const int CoreCap = 20;

    // ===== Архетипы =====

    /// <summary>Архетипы текущего сеттинга: ключи и механика общие, названия, снаряжение и идеи классов свои.</summary>
    public static ClassArchetype[] Archetypes => Lang.IsEn
        ? Genre.Pick(FantasyArchetypesEn, CyberArchetypesEn, ModernArchetypesEn)
        : Genre.Pick(FantasyArchetypes, CyberArchetypes, ModernArchetypes);

    /// <summary>Английские архетипы: та же механика, английские названия, вопросы и идеи классов.</summary>
    private static readonly ClassArchetype[] ModernArchetypesEn =
    {
        new("tank", "Bodyguard", "Cover the team and take the hits", "🛡️", 10, 4, 2,
            new[] { DndStat.Con, DndStat.Str, DndStat.Wis, DndStat.Dex, DndStat.Cha, DndStat.Int }, 17, "md_pistol", "md_police_vest", false,
            new[] { "bodyguard", "ex-riot cop", "security guard", "bouncer", "special forces operator" }),
        new("striker", "Brawler", "Break enemies in close combat", "⚔️", 10, 4, 2,
            new[] { DndStat.Str, DndStat.Con, DndStat.Dex, DndStat.Wis, DndStat.Cha, DndStat.Int }, 16, "md_axe", "md_vest_hidden", false,
            new[] { "MMA fighter", "thug", "paratrooper", "ex-boxer", "gangster" }),
        new("ranged", "Shooter", "Pour fire on the enemies from afar", "🎯", 8, 4, 2,
            new[] { DndStat.Dex, DndStat.Wis, DndStat.Con, DndStat.Str, DndStat.Int, DndStat.Cha }, 14, "md_rifle", "md_plate_carrier", false,
            new[] { "sniper", "marksman", "hunter", "contractor", "PMC operator" }),
        new("skirmisher", "Infiltrator", "Strike unseen, slip in and vanish", "🗡️", 8, 4, 2,
            new[] { DndStat.Dex, DndStat.Con, DndStat.Cha, DndStat.Int, DndStat.Wis, DndStat.Str }, 14, "md_combat_knife", "md_leather", false,
            new[] { "thief", "hitman", "burglar", "scout", "free runner" }),
        new("caster", "Mystic / hacker", "Cast spells or hack machines and networks", "🔮", 6, 8, 4,
            new[] { DndStat.Int, DndStat.Con, DndStat.Dex, DndStat.Wis, DndStat.Cha, DndStat.Str }, 12, "md_laptop", "md_hoodie", true,
            new[] { "city mage", "witch", "occultist", "exorcist", "medium", "hacker", "IT guy", "analyst" }),
        new("healer", "Medic", "Heal and pull your people out", "✚", 8, 8, 4,
            new[] { DndStat.Wis, DndStat.Con, DndStat.Str, DndStat.Cha, DndStat.Dex, DndStat.Int }, 15, "md_pistol", "md_vest_hidden", true,
            new[] { "paramedic", "surgeon", "nurse", "combat medic", "veterinarian" }),
        new("support", "Techie", "Boost the team with gear and drones", "🔧", 8, 6, 3,
            new[] { DndStat.Cha, DndStat.Dex, DndStat.Con, DndStat.Wis, DndStat.Int, DndStat.Str }, 14, "md_drone_ctrl", "md_vest_hidden", true,
            new[] { "mechanic", "drone operator", "driver", "signals specialist", "sapper" }),
        new("controller", "Specialist", "Pin down and take enemies out of the fight", "🌀", 6, 8, 4,
            new[] { DndStat.Int, DndStat.Wis, DndStat.Con, DndStat.Dex, DndStat.Cha, DndStat.Str }, 12, "md_taser", "md_leather", true,
            new[] { "negotiator", "police officer", "psychologist", "interrogation specialist", "tactician" }),
    };

    private static readonly ClassArchetype[] CyberArchetypesEn =
    {
        new("tank", "Defender", "Cover the crew and draw fire", "🛡️", 10, 4, 2,
            new[] { DndStat.Con, DndStat.Str, DndStat.Wis, DndStat.Dex, DndStat.Cha, DndStat.Int }, 17, "cy_pistol", "cy_riot", false,
            new[] { "bodyguard", "ex-special forces", "corporate guard", "bouncer", "trooper" }),
        new("striker", "Brawler", "Break enemies in close combat", "⚔️", 10, 4, 2,
            new[] { DndStat.Str, DndStat.Con, DndStat.Dex, DndStat.Wis, DndStat.Cha, DndStat.Int }, 16, "cy_hammer", "cy_tactical", false,
            new[] { "solo", "street fighter", "cyber-samurai", "arena gladiator", "gang heavy" }),
        new("ranged", "Shooter", "Pour fire on the enemies from afar", "🎯", 8, 4, 2,
            new[] { DndStat.Dex, DndStat.Wis, DndStat.Con, DndStat.Str, DndStat.Int, DndStat.Cha }, 14, "cy_rifle", "cy_vest", false,
            new[] { "sniper", "gunslinger", "bounty hunter", "ex-military", "mercenary" }),
        new("skirmisher", "Runner", "Strike from the shadows, slip in and vanish", "🗡️", 8, 4, 2,
            new[] { DndStat.Dex, DndStat.Con, DndStat.Cha, DndStat.Int, DndStat.Wis, DndStat.Str }, 14, "cy_katana", "cy_jacket", false,
            new[] { "runner", "thief", "cyber-ninja", "spy", "smuggler" }),
        new("caster", "Netrunner", "Hack enemies, networks and implants", "💻", 6, 8, 4,
            new[] { DndStat.Int, DndStat.Con, DndStat.Dex, DndStat.Wis, DndStat.Cha, DndStat.Str }, 12, "cy_deck", "cy_hoodie", true,
            new[] { "netrunner", "hacker", "cracker", "digital ghost", "cyber-witch" }),
        new("healer", "Medtech", "Heal and pull your people out", "✚", 8, 8, 4,
            new[] { DndStat.Wis, DndStat.Con, DndStat.Str, DndStat.Cha, DndStat.Dex, DndStat.Int }, 15, "cy_pistol", "cy_vest", true,
            new[] { "medtech", "ripperdoc", "field medic", "trauma doc", "pharmacist" }),
        new("support", "Tech", "Boost the crew with gadgets and drones", "🔧", 8, 6, 3,
            new[] { DndStat.Cha, DndStat.Dex, DndStat.Con, DndStat.Wis, DndStat.Int, DndStat.Str }, 14, "cy_drone_remote", "cy_vest", true,
            new[] { "techie", "drone operator", "engineer", "mechanic", "fixer" }),
        new("controller", "Controller", "Jam systems, pin down and deceive", "🌀", 6, 8, 4,
            new[] { DndStat.Int, DndStat.Wis, DndStat.Con, DndStat.Dex, DndStat.Cha, DndStat.Str }, 12, "cy_deck", "cy_jacket_neon", true,
            new[] { "EW specialist", "holographer", "psychotech", "jammer", "manipulator" }),
    };

    private static readonly ClassArchetype[] FantasyArchetypesEn =
    {
        new("tank", "Defender", "Protect the party and take the hits", "🛡️", 10, 4, 2,
            new[] { DndStat.Con, DndStat.Str, DndStat.Wis, DndStat.Dex, DndStat.Cha, DndStat.Int }, 17, "long_sword", "chain_mail", false,
            new[] { "warden", "paladin", "knight", "templar", "shield-keeper" }),
        new("striker", "Fighter", "Deal damage in melee", "⚔️", 10, 4, 2,
            new[] { DndStat.Str, DndStat.Con, DndStat.Dex, DndStat.Wis, DndStat.Cha, DndStat.Int }, 16, "greatsword", "scale_mail", false,
            new[] { "warrior", "barbarian", "mercenary", "duelist", "berserker" }),
        new("ranged", "Marksman", "Deal damage from afar", "🏹", 8, 4, 2,
            new[] { DndStat.Dex, DndStat.Wis, DndStat.Con, DndStat.Str, DndStat.Int, DndStat.Cha }, 14, "longbow", "leather_armour", false,
            new[] { "ranger", "archer", "hunter", "crossbowman", "knife thrower" }),
        new("skirmisher", "Rogue", "Strike from the shadows, poisons and critical hits", "🗡️", 8, 4, 2,
            new[] { DndStat.Dex, DndStat.Con, DndStat.Cha, DndStat.Int, DndStat.Wis, DndStat.Str }, 14, "rapier", "leather_armour", false,
            new[] { "rogue", "assassin", "scout", "smuggler", "blade dancer" }),
        new("caster", "Mage", "Smite enemies with magic and the elements", "🔮", 6, 8, 4,
            new[] { DndStat.Int, DndStat.Con, DndStat.Dex, DndStat.Wis, DndStat.Cha, DndStat.Str }, 12, "mage_staff", "mage_robe", true,
            new[] { "mage", "sorcerer", "warlock", "elementalist", "necromancer" }),
        new("healer", "Healer", "Heal and save allies", "✚", 8, 8, 4,
            new[] { DndStat.Wis, DndStat.Con, DndStat.Str, DndStat.Cha, DndStat.Dex, DndStat.Int }, 15, "mace", "chain_mail", true,
            new[] { "priest", "healer", "druid", "herbalist", "nun" }),
        new("support", "Support", "Empower allies and weaken enemies", "✨", 8, 6, 3,
            new[] { DndStat.Cha, DndStat.Dex, DndStat.Con, DndStat.Wis, DndStat.Int, DndStat.Str }, 14, "rapier", "leather_armour", true,
            new[] { "bard", "standard-bearer", "alchemist", "shaman", "mystic" }),
        new("controller", "Controller", "Bind, put to sleep and confuse enemies", "🌀", 6, 8, 4,
            new[] { DndStat.Int, DndStat.Wis, DndStat.Con, DndStat.Dex, DndStat.Cha, DndStat.Str }, 12, "wand", "robe", true,
            new[] { "illusionist", "binder", "witch", "mentalist", "chronomancer" }),
    };

    private static readonly ClassArchetype[] ModernArchetypes =
    {
        new("tank", "Телохранитель", "Прикрывать команду и держать удар", "🛡️", 10, 4, 2,
            new[] { DndStat.Con, DndStat.Str, DndStat.Wis, DndStat.Dex, DndStat.Cha, DndStat.Int }, 17, "md_pistol", "md_police_vest", false,
            new[] { "телохранитель", "бывший омоновец", "охранник", "вышибала", "спецназовец" }),
        new("striker", "Боец", "Ломать врагов в ближнем бою", "⚔️", 10, 4, 2,
            new[] { DndStat.Str, DndStat.Con, DndStat.Dex, DndStat.Wis, DndStat.Cha, DndStat.Int }, 16, "md_axe", "md_vest_hidden", false,
            new[] { "боец ММА", "громила", "десантник", "бывший боксёр", "бандит" }),
        new("ranged", "Стрелок", "Поливать врагов огнём издалека", "🎯", 8, 4, 2,
            new[] { DndStat.Dex, DndStat.Wis, DndStat.Con, DndStat.Str, DndStat.Int, DndStat.Cha }, 14, "md_rifle", "md_plate_carrier", false,
            new[] { "снайпер", "стрелок", "охотник", "контрактник", "боец ЧВК" }),
        new("skirmisher", "Ловкач", "Бить исподтишка, проникать и исчезать", "🗡️", 8, 4, 2,
            new[] { DndStat.Dex, DndStat.Con, DndStat.Cha, DndStat.Int, DndStat.Wis, DndStat.Str }, 14, "md_combat_knife", "md_leather", false,
            new[] { "вор", "киллер", "домушник", "разведчик", "паркурщик" }),
        new("caster", "Мистик / хакер", "Колдовать или взламывать технику и сети", "🔮", 6, 8, 4,
            new[] { DndStat.Int, DndStat.Con, DndStat.Dex, DndStat.Wis, DndStat.Cha, DndStat.Str }, 12, "md_laptop", "md_hoodie", true,
            new[] { "городской маг", "ведьма", "оккультист", "экзорцист", "медиум", "хакер", "айтишник", "аналитик" }),
        new("healer", "Медик", "Лечить и вытаскивать своих", "✚", 8, 8, 4,
            new[] { DndStat.Wis, DndStat.Con, DndStat.Str, DndStat.Cha, DndStat.Dex, DndStat.Int }, 15, "md_pistol", "md_vest_hidden", true,
            new[] { "фельдшер", "хирург", "медсестра", "военный медик", "ветеринар" }),
        new("support", "Технарь", "Усиливать команду техникой и дронами", "🔧", 8, 6, 3,
            new[] { DndStat.Cha, DndStat.Dex, DndStat.Con, DndStat.Wis, DndStat.Int, DndStat.Str }, 14, "md_drone_ctrl", "md_vest_hidden", true,
            new[] { "механик", "дронщик", "водитель", "связист", "сапёр" }),
        new("controller", "Специалист", "Сковывать и выводить врагов из строя", "🌀", 6, 8, 4,
            new[] { DndStat.Int, DndStat.Wis, DndStat.Con, DndStat.Dex, DndStat.Cha, DndStat.Str }, 12, "md_taser", "md_leather", true,
            new[] { "переговорщик", "полицейский", "психолог", "специалист по допросам", "тактик" }),
    };

    private static readonly ClassArchetype[] CyberArchetypes =
    {
        new("tank", "Защитник", "Прикрывать команду и держать огонь", "🛡️", 10, 4, 2,
            new[] { DndStat.Con, DndStat.Str, DndStat.Wis, DndStat.Dex, DndStat.Cha, DndStat.Int }, 17, "cy_pistol", "cy_riot", false,
            new[] { "телохранитель", "бывший спецназовец", "охранник корпорации", "вышибала", "штурмовик" }),
        new("striker", "Боец", "Ломать врагов в ближнем бою", "⚔️", 10, 4, 2,
            new[] { DndStat.Str, DndStat.Con, DndStat.Dex, DndStat.Wis, DndStat.Cha, DndStat.Int }, 16, "cy_hammer", "cy_tactical", false,
            new[] { "соло", "уличный боец", "киберсамурай", "гладиатор арены", "громила банды" }),
        new("ranged", "Стрелок", "Поливать врагов огнём издалека", "🎯", 8, 4, 2,
            new[] { DndStat.Dex, DndStat.Wis, DndStat.Con, DndStat.Str, DndStat.Int, DndStat.Cha }, 14, "cy_rifle", "cy_vest", false,
            new[] { "снайпер", "стрелок", "охотник за головами", "бывший военный", "наёмник" }),
        new("skirmisher", "Раннер", "Бить из тени, проникать и исчезать", "🗡️", 8, 4, 2,
            new[] { DndStat.Dex, DndStat.Con, DndStat.Cha, DndStat.Int, DndStat.Wis, DndStat.Str }, 14, "cy_katana", "cy_jacket", false,
            new[] { "раннер", "вор", "киберниндзя", "шпион", "контрабандист" }),
        new("caster", "Нетраннер", "Взламывать врагов, сети и импланты", "💻", 6, 8, 4,
            new[] { DndStat.Int, DndStat.Con, DndStat.Dex, DndStat.Wis, DndStat.Cha, DndStat.Str }, 12, "cy_deck", "cy_hoodie", true,
            new[] { "нетраннер", "хакер", "взломщик", "цифровой призрак", "кибер-ведьма" }),
        new("healer", "Медтех", "Лечить и вытаскивать своих", "✚", 8, 8, 4,
            new[] { DndStat.Wis, DndStat.Con, DndStat.Str, DndStat.Cha, DndStat.Dex, DndStat.Int }, 15, "cy_pistol", "cy_vest", true,
            new[] { "медтех", "риппердок", "полевой медик", "травматолог", "фармацевт" }),
        new("support", "Техник", "Усиливать команду гаджетами и дронами", "🔧", 8, 6, 3,
            new[] { DndStat.Cha, DndStat.Dex, DndStat.Con, DndStat.Wis, DndStat.Int, DndStat.Str }, 14, "cy_drone_remote", "cy_vest", true,
            new[] { "техник", "дронщик", "инженер", "механик", "фиксер" }),
        new("controller", "Контролёр", "Глушить системы, сковывать и обманывать", "🌀", 6, 8, 4,
            new[] { DndStat.Int, DndStat.Wis, DndStat.Con, DndStat.Dex, DndStat.Cha, DndStat.Str }, 12, "cy_deck", "cy_jacket_neon", true,
            new[] { "специалист РЭБ", "голографист", "психотехник", "глушильщик", "манипулятор" }),
    };

    private static readonly ClassArchetype[] FantasyArchetypes =
    {
        new("tank", "Защитник", "Защищать группу и держать удар", "🛡️", 10, 4, 2,
            new[] { DndStat.Con, DndStat.Str, DndStat.Wis, DndStat.Dex, DndStat.Cha, DndStat.Int }, 17, "long_sword", "chain_mail", false,
            new[] { "страж", "паладин", "рыцарь", "храмовник", "хранитель щита" }),
        new("striker", "Боец", "Наносить урон в ближнем бою", "⚔️", 10, 4, 2,
            new[] { DndStat.Str, DndStat.Con, DndStat.Dex, DndStat.Wis, DndStat.Cha, DndStat.Int }, 16, "greatsword", "scale_mail", false,
            new[] { "воин", "варвар", "наёмник", "дуэлянт", "берсерк" }),
        new("ranged", "Стрелок", "Наносить урон издалека", "🏹", 8, 4, 2,
            new[] { DndStat.Dex, DndStat.Wis, DndStat.Con, DndStat.Str, DndStat.Int, DndStat.Cha }, 14, "longbow", "leather_armour", false,
            new[] { "следопыт", "лучник", "охотник", "арбалетчик", "метатель ножей" }),
        new("skirmisher", "Ловкач", "Бить из тени, яды и критические удары", "🗡️", 8, 4, 2,
            new[] { DndStat.Dex, DndStat.Con, DndStat.Cha, DndStat.Int, DndStat.Wis, DndStat.Str }, 14, "rapier", "leather_armour", false,
            new[] { "плут", "ассасин", "разведчик", "контрабандист", "танцор клинков" }),
        new("caster", "Маг", "Разить врагов магией и стихиями", "🔮", 6, 8, 4,
            new[] { DndStat.Int, DndStat.Con, DndStat.Dex, DndStat.Wis, DndStat.Cha, DndStat.Str }, 12, "mage_staff", "mage_robe", true,
            new[] { "маг", "чародей", "колдун", "элементалист", "некромант" }),
        new("healer", "Целитель", "Лечить и спасать союзников", "✚", 8, 8, 4,
            new[] { DndStat.Wis, DndStat.Con, DndStat.Str, DndStat.Cha, DndStat.Dex, DndStat.Int }, 15, "mace", "chain_mail", true,
            new[] { "жрец", "целитель", "друид", "травница", "монахиня" }),
        new("support", "Поддержка", "Усиливать союзников и ослаблять врагов", "✨", 8, 6, 3,
            new[] { DndStat.Cha, DndStat.Dex, DndStat.Con, DndStat.Wis, DndStat.Int, DndStat.Str }, 14, "rapier", "leather_armour", true,
            new[] { "бард", "знаменосец", "алхимик", "шаман", "мистик" }),
        new("controller", "Контролёр", "Сковывать, усыплять и путать врагов", "🌀", 6, 8, 4,
            new[] { DndStat.Int, DndStat.Wis, DndStat.Con, DndStat.Dex, DndStat.Cha, DndStat.Str }, 12, "wand", "robe", true,
            new[] { "иллюзионист", "чародейка оков", "ведьма", "менталист", "хрономант" }),
    };

    public static ClassArchetype Archetype(string? key) =>
        Archetypes.FirstOrDefault(a => a.Key.Equals((key ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) ?? GuessArchetype(key);

    private static readonly (string Key, string[] Stems)[] ClassStems =
    {
        ("healer", new[] { "жрец", "целит", "лекар", "друид", "травни", "клирик", "healer", "cleric", "priest", "монах", "монахин", "медтех", "медик", "риппер", "врач", "фармац", "травматолог" }),
        ("support", new[] { "техник", "дронщик", "инженер", "механик", "фиксер", "технар", "связист", "сапёр", "водител" }),
        ("healer", new[] { "фельдш", "хирург", "медсестр", "медик", "ветерин" }),
        ("caster", new[] { "айтишн", "аналитик", "взломщик сейф" }),
        ("controller", new[] { "переговор", "психолог", "допрос", "тактик", "детектив" }),
        ("skirmisher", new[] { "домушн", "киллер", "паркур", "карманн" }),
        ("ranged", new[] { "контрактн", "чвк", "охотник" }),
        ("tank", new[] { "омонов", "охранник" }),
        ("striker", new[] { "ммa", "ммa", "боксёр", "боксер", "десант", "бандит" }),
        ("caster", new[] { "нетран", "хакер", "взломщ", "цифров" }),
        ("controller", new[] { "рэб", "голограф", "психотех", "глушил", "манипул" }),
        ("skirmisher", new[] { "раннер", "ниндзя", "шпион" }),
        ("ranged", new[] { "снайпер", "охотник за голов" }),
        ("tank", new[] { "телохран", "спецназ", "вышибал", "штурмов" }),
        ("striker", new[] { "соло", "самура", "гладиатор", "громил" }),
        ("controller", new[] { "иллюз", "ведьм", "менталист", "хроно", "оков", "enchant", "witch" }),
        ("caster", new[] { "маг", "чарод", "колдун", "некром", "волшеб", "элемент", "чернокниж", "wizard", "sorcer", "warlock", "mage" }),
        ("support", new[] { "бард", "знамен", "алхим", "шаман", "мистик", "bard", "alchem" }),
        ("ranged", new[] { "следоп", "лучн", "охотн", "стрел", "арбалет", "ranger", "archer", "hunter", "пилот" }),
        ("skirmisher", new[] { "плут", "вор", "убийц", "ассасин", "разведч", "rogue", "thief", "assassin", "хакер", "детектив" }),
        ("tank", new[] { "паладин", "рыцар", "страж", "храмовн", "защитн", "paladin", "knight", "guard" }),
        ("striker", new[] { "воин", "варвар", "наёмн", "наемн", "берсерк", "дуэл", "fighter", "barbarian", "warrior" }),
        // Английские названия классов (английский интерфейс).
        ("healer", new[] { "druid", "herbalist", "nun", "monk", "medtech", "medic", "ripperdoc", "doctor", "doc", "pharmac", "paramedic", "surgeon", "nurse", "veterinar" }),
        ("support", new[] { "techie", "tech", "drone", "engineer", "mechanic", "fixer", "signal", "sapper", "driver", "standard-bearer", "mystic" }),
        ("controller", new[] { "negotiat", "psycholog", "interrogat", "tactician", "detective", "illusion", "mentalist", "chrono", "binder", "jammer", "holograph", "psychotech", "manipulat", " ew " }),
        ("caster", new[] { "netrun", "hacker", "cracker", "digital", "analyst", "occult", "exorcist", "medium", "necroman", "elementalist" }),
        ("skirmisher", new[] { "runner", "ninja", "spy", "burglar", "hitman", "parkour", "free runner", "pickpocket", "scout", "smuggler", "blade dancer" }),
        ("ranged", new[] { "sniper", "marksman", "gunslinger", "bounty hunter", "contractor", "pmc", "crossbow", "knife thrower", "shooter" }),
        ("tank", new[] { "bodyguard", "special forces", "bouncer", "trooper", "riot", "security", "warden", "templar", "shield", "defender" }),
        ("striker", new[] { "solo", "samurai", "gladiator", "brawler", "thug", "heavy", "mma", "boxer", "paratrooper", "gangster", "mercenary", "duelist", "berserk" }),
    };

    /// <summary>Архетип по названию класса («Некромант» → caster). Неизвестное — боец.</summary>
    public static ClassArchetype GuessArchetype(string? charClass)
    {
        var c = (charClass ?? "").Trim().ToLowerInvariant();
        var hit = ClassStems.FirstOrDefault(s => s.Stems.Any(c.Contains));
        return Archetypes.First(a => a.Key == (hit.Key ?? "striker"));
    }

    /// <summary>Архетип героя: сохранённый или угаданный по классу.</summary>
    public static ClassArchetype ArchetypeOf(ICombatant who) =>
        string.IsNullOrWhiteSpace(who.Role) ? GuessArchetype(who.CharClass) : Archetype(who.Role);

    // ===== Уровни =====

    public static int ParseLevel(string? level)
    {
        var digits = new string((level ?? "").Trim().TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var l) ? Math.Clamp(l, 1, 99) : 1;
    }

    /// <summary>Сколько всего опыта нужно, чтобы достичь уровня.</summary>
    public static int XpFor(int level) => Math.Max(0, level - 1) * XpPerLevel;

    public static int LevelForXp(int xp) => 1 + Math.Max(0, xp) / XpPerLevel;

    /// <summary>Бонус мастерства: +2 на 1–4, +3 на 5–8 … без потолка (эндгейм продолжает рост).</summary>
    public static int Proficiency(int level) => 2 + (Math.Max(1, level) - 1) / 4;

    /// <summary>Кости мощи: сколько раз бросается кость оружия или заклинания (как рост заговоров в 5e).</summary>
    public static int PowerDice(int level) => 1 + Math.Max(1, level) / 5;

    /// <summary>Боевой опыт: бонус к КБ от уровня (+1 на 6, 12, 18 …).</summary>
    public static int DefenseBonus(int level) => Math.Max(1, level) / 6;

    /// <summary>Максимальная ступень навыка на уровне: 1 на 1–4, 2 на 5–8 … 5 с 17-го.</summary>
    public static int MaxSkillRank(int level) => Math.Min(5, 1 + (Math.Max(1, level) - 1) / 4);

    /// <summary>Уровни, на которых растут характеристики (+2 очка: +2 к одной или +1 к двум).</summary>
    public static bool IsStatLevel(int level) => level is 4 or 8 or 12 or 16 or 19 || (level > CoreCap && level % 4 == 0);

    public static int HpPerLevel(ClassArchetype a, int conMod) => Math.Max(1, a.HitDie / 2 + 1 + conMod);

    public static int HpAtLevel(ClassArchetype a, int conMod, int level) =>
        Math.Max(1, a.HitDie + conMod) + (Math.Max(1, level) - 1) * HpPerLevel(a, conMod);

    /// <summary>Герой живучее базового архетипа вдвое, спутник — в полтора раза.</summary>
    public static int HeroHpPerLevel(ClassArchetype a, int conMod) => HpPerLevel(a, conMod) * 2;
    public static int CompanionHpPerLevel(ClassArchetype a, int conMod) => ScaleCompanionHp(HpPerLevel(a, conMod));
    public static int HeroHpAtLevel(ClassArchetype a, int conMod, int level) => HpAtLevel(a, conMod, level) * 2;
    public static int CompanionHpAtLevel(ClassArchetype a, int conMod, int level) => ScaleCompanionHp(HpAtLevel(a, conMod, level));
    public static int ScaleCompanionHp(int hp) => Math.Max(0, (int)Math.Round(hp * 1.5, MidpointRounding.AwayFromZero));

    public static int ManaAtLevel(ClassArchetype a, int keyMod, int level) =>
        Math.Max(0, a.ManaBase + Math.Max(0, keyMod) * (a.Spellcaster ? 2 : 1) + (Math.Max(1, level) - 1) * a.ManaPerLevel);

    private static readonly System.Text.RegularExpressions.Regex PowerDiceRx =
        new(@"(\d+(?:[.,]\d+)?)\s*[КK](?![\p{L}])", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>«1.5К огнём» на 10-м уровне (К = 3d8) → «1.5К (=5d8) огнём»: навыки растут вместе с уровнем владельца.</summary>
    public static string ExplainPowerDice(string? text, int level)
    {
        var power = PowerDice(level);
        return PowerDiceRx.Replace(text ?? "", m =>
        {
            var n = double.Parse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
            return $"{m.Value} (={Math.Max(1, (int)Math.Ceiling(n * power))}d8)";
        });
    }

    /// <summary>Ориентир урона навыка ступени R в костях мощи К (одна цель / область).</summary>
    public static string SkillBudget(int rank) => Lang.IsEn ? rank switch
    {
        1 => "1K to one target (or a weak effect), 2–4 mana",
        2 => "1.5K to one target or 1K to 2–3 targets, 4–7 mana",
        3 => "2K to one target or 1.5K in an area; a shield up to 30% HP; cooldown 2 rounds; 7–12 mana",
        4 => "3K to one target or 2K in an area; a shield up to 50% HP; resurrection with 1 HP; once per fight; 12–18 mana",
        _ => "4K to one target or 3K across the scene; a legendary effect; once a day; 18–30 mana",
    } : rank switch
    {
        1 => "1К одной цели (или слабый эффект), 2–4 маны",
        2 => "1.5К одной цели или 1К по 2–3 целям, 4–7 маны",
        3 => "2К одной цели или 1.5К по области; щит до 30% ХП; перезарядка 2 раунда; 7–12 маны",
        4 => "3К одной цели или 2К по области; щит до 50% ХП; воскрешение с 1 ХП; раз в бой; 12–18 маны",
        _ => "4К одной цели или 3К по сцене; легендарный эффект; раз в день; 18–30 маны",
    };

    // ===== Стандартный герой (эталон калибровки) =====

    private static int StdStatMod(int level) => level < 4 ? 3 : level < 8 ? 4 : 5;

    public static int StdPcAttack(int level) => StdStatMod(level) + Proficiency(level);

    public static int StdPcAc(int level) => 15 + DefenseBonus(level);

    public static int StdPcHp(int level) => 11 + (int)Math.Round(5.5 * (Math.Max(1, level) - 1));

    // ===== Противники =====

    public static readonly string[] Roles = { "minion", "ordinary", "elite", "quest_boss", "dungeon_boss", "arc_boss" };

    public static string RoleTitle(string? role) => role switch
    {
        "minion" => Lang.T("прислужник", "minion"),
        "elite" => Lang.T("элита", "elite"),
        "quest_boss" => Lang.T("босс задания", "quest boss"),
        "dungeon_boss" => Lang.T("босс данжа", "dungeon boss"),
        "arc_boss" => Lang.T("босс арки", "arc boss"),
        _ => Lang.T("рядовой", "rank-and-file"),
    };

    /// <summary>Множители ХП и урона роли. Босс = N рядовых по «квадратному закону»: ХП ×N, урон ×(N+1)/2.</summary>
    private static (double Hp, double Dmg, int Ac, int Atk, int Stagger, int Legendary) RoleMods(string role) => role switch
    {
        "minion" => (0.3, 0.6, -1, 0, 0, 0),
        "elite" => (2, 1.5, 1, 1, 2, 0),
        "quest_boss" => (4, 2.4, 2, 2, 3, 0),
        "dungeon_boss" => (6, 3.1, 2, 2, 3, 1),
        "arc_boss" => (7, 3.0, 3, 3, 4, 2),
        _ => (1, 1, 0, 0, 0, 0),
    };

    public static (string Key, string Title)[] MonsterArchetypes => Lang.IsEn ? MonsterArchetypesEn : MonsterArchetypesRu;

    private static readonly (string Key, string Title)[] MonsterArchetypesRu =
    {
        ("standard", "обычный боец"), ("brute", "громила: много ХП и урона, низкий КБ"),
        ("skirmisher", "ловкач: высокий КБ и точность"), ("defender", "защитник: очень высокий КБ, слабый урон"),
        ("caster", "заклинатель: хрупкий, сильный урон и эффекты по спасброскам"), ("sniper", "стрелок: бьёт издалека, хрупкий"),
    };

    private static readonly (string Key, string Title)[] MonsterArchetypesEn =
    {
        ("standard", "standard fighter"), ("brute", "brute: lots of HP and damage, low AC"),
        ("skirmisher", "skirmisher: high AC and accuracy"), ("defender", "defender: very high AC, weak damage"),
        ("caster", "caster: fragile, strong damage and saving-throw effects"), ("sniper", "sniper: strikes from afar, fragile"),
    };

    /// <summary>Поправки архетипа, подобранные так, чтобы общая опасность оставалась ≈1 (эффективные ХП × урон).</summary>
    private static (double Hp, double Dmg, int Ac, int Atk, int Die) ArchMods(string arch) => arch switch
    {
        "brute" => (1.2, 1.1, -2, -1, 10),
        "skirmisher" => (0.9, 0.95, 1, 1, 6),
        "defender" => (1.1, 0.7, 3, 0, 8),
        "caster" => (0.8, 1.4, -2, 0, 8),
        "sniper" => (0.85, 1.2, -1, 1, 8),
        _ => (1, 1, 0, 0, 8),
    };

    public static string NormalizeRole(string? role)
    {
        var r = (role ?? "").Trim().ToLowerInvariant();
        return r switch
        {
            "minion" or "прислужник" or "миньон" or "lackey" => "minion",
            "elite" or "элита" => "elite",
            "boss" or "quest_boss" or "quest boss" or "босс" => "quest_boss",
            "dungeon_boss" or "dungeon boss" => "dungeon_boss",
            "arc_boss" or "arc boss" => "arc_boss",
            _ => "ordinary",
        };
    }

    public static string NormalizeMonsterArchetype(string? arch)
    {
        var a = (arch ?? "").Trim().ToLowerInvariant();
        return MonsterArchetypes.Any(x => x.Key == a) ? a : a switch
        {
            "громила" or "танк" or "tank" or "bruiser" => "brute",
            "ловкач" => "skirmisher",
            "защитник" => "defender",
            "маг" or "заклинатель" or "шаман" or "mage" or "spellcaster" or "shaman" or "wizard" => "caster",
            "стрелок" or "лучник" or "archer" or "shooter" or "ranged" => "sniper",
            _ => "standard",
        };
    }

    /// <summary>Статблок противника: всё, что нужно для честного боя.</summary>
    public sealed record StatBlock(
        int Level, string Role, string Archetype, int Hp, int Ac, int Attack, int Attacks,
        int Dice, int Die, int DamageBonus, int SaveDc, int Stagger, int Legendary)
    {
        public double AvgHit => Dice * (Die + 1) / 2.0 + DamageBonus;

        public string DamageText => $"{Dice}d{Die}{(DamageBonus > 0 ? $"+{DamageBonus}" : "")}";

        public string AttackText(string? damageType) =>
            Lang.T($"{(Attacks > 1 ? $"{Attacks} атаки" : "атака")} {Attack:+#;-#;+0}, урон {DamageText}", $"{(Attacks > 1 ? $"{Attacks} attacks" : "attack")} {Attack:+#;-#;+0}, damage {DamageText}") +
            (string.IsNullOrWhiteSpace(damageType) ? "" : $" {damageType!.Trim()}");
    }

    private static double HitChance(int attack, int ac) => Math.Clamp((21.0 - (ac - attack)) / 20.0, 0.05, 0.95);

    /// <summary>ХП рядового врага уровня L: ≈2.6 раунда ударов стандартного героя (сглажено).</summary>
    private static double OrdinaryHp(int level) => level <= 5 ? 12 + 3 * level : 27 + 2.2 * (level - 5);

    /// <summary>Урон рядового за раунд: ~16% ХП стандартного героя.</summary>
    private static double OrdinaryDpr(int level) => Math.Max(1.9, 0.16 * StdPcHp(level));

    public static StatBlock Monster(int level, string? role = null, string? archetype = null)
    {
        // Уровни 0 и ниже — ослабленные существа для одинокого героя 1-го уровня: ХП и урон ×0.78 за каждую ступень вниз.
        var shown = Math.Clamp(level, -2, 60);
        var weak = shown < 1 ? Math.Pow(0.78, 1 - shown) : 1.0;
        level = Math.Max(1, shown);
        var r = NormalizeRole(role);
        var a = NormalizeMonsterArchetype(archetype);
        var rm = RoleMods(r);
        var am = ArchMods(a);

        var hp = Math.Max(3, (int)Math.Round(OrdinaryHp(level) * rm.Hp * am.Hp * weak));
        var ac = StdPcAttack(level) + 8 + rm.Ac + am.Ac;
        var atk = 5 + level / 4 + rm.Atk + am.Atk;
        var attacks = (level < 7 ? 1 : level < 16 ? 2 : 3) + (r is "quest_boss" or "dungeon_boss" or "arc_boss" ? 1 : 0);
        var dpr = OrdinaryDpr(level) * rm.Dmg * am.Dmg * weak;
        var perHit = Math.Max(r == "minion" ? 2.5 : 3.5, dpr / HitChance(atk, StdPcAc(level)) / attacks);

        var die = am.Die;
        var avgDie = (die + 1) / 2.0;
        var dice = Math.Max(1, (int)Math.Round(perHit * 0.65 / avgDie));
        var bonus = (int)Math.Round(perHit - dice * avgDie);
        while (bonus < 0 && dice > 1)
        {
            dice--;
            bonus = (int)Math.Round(perHit - dice * avgDie);
        }

        // Слабый удар (низкие уровни, прислужники): одна кость помельче, чтобы средний урон не превышал расчётный.
        if (bonus < 0)
        {
            die = new[] { 12, 10, 8, 6, 4 }.FirstOrDefault(d => (d + 1) / 2.0 <= perHit + 0.01, 4);
            bonus = Math.Max(0, (int)Math.Round(perHit - (die + 1) / 2.0));
        }

        var dc = 8 + Proficiency(level) + StdStatMod(level) + (a == "caster" ? 1 : 0) + (rm.Atk > 0 ? 1 : 0);
        return new StatBlock(shown, r, a, hp, ac - (shown < 1 ? 1 - shown : 0), atk - (shown < 1 ? 1 - shown : 0), attacks, dice, die, Math.Max(0, bonus), dc, rm.Stagger, rm.Legendary);
    }

    // ===== Бойцы для симуляции =====

    /// <summary>Участник симуляции боя.</summary>
    public sealed record Fighter(string Name, int Hp, int Ac, int Attack, int Attacks, int Dice, int Die, int Bonus, double Boost = 1.0);

    /// <summary>Стандартный герой уровня L (эталон: оружие d8, характеристика 16–20, без магических вещей).</summary>
    public static Fighter StandardPc(int level) =>
        new("эталон", StdPcHp(level), StdPcAc(level), StdPcAttack(level), 1, PowerDice(level), 8, StdStatMod(level), 1.25);

    public static Fighter FromBlock(string name, StatBlock b) => new(name, b.Hp, b.Ac, b.Attack, b.Attacks, b.Dice, b.Die, b.DamageBonus);

    /// <summary>Итог симуляции встречи.</summary>
    public sealed record Outcome(double Rounds, double Loss, double DownChance, double WipeChance)
    {
        public string Threat => ThreatOf(Loss, WipeChance);
    }

    public static (string Key, string Title, double Center)[] Threats => Lang.IsEn ? ThreatsEn : ThreatsRu;

    private static readonly (string Key, string Title, double Center)[] ThreatsRu =
    {
        ("trivial", "тривиальная", 0.06), ("low", "лёгкая", 0.16), ("moderate", "средняя", 0.30),
        ("severe", "тяжёлая", 0.50), ("extreme", "смертельная", 0.72), ("deadly", "запредельная", 1.0),
    };

    private static readonly (string Key, string Title, double Center)[] ThreatsEn =
    {
        ("trivial", "trivial", 0.06), ("low", "low", 0.16), ("moderate", "moderate", 0.30),
        ("severe", "severe", 0.50), ("extreme", "extreme", 0.72), ("deadly", "beyond deadly", 1.0),
    };

    public static string ThreatOf(double loss, double wipe) =>
        wipe >= 0.35 || loss >= 0.88 ? "deadly" :
        loss >= 0.60 || wipe >= 0.15 ? "extreme" :
        loss >= 0.40 ? "severe" :
        loss >= 0.22 ? "moderate" :
        loss >= 0.10 ? "low" : "trivial";

    public static string ThreatTitle(string key) => Threats.FirstOrDefault(t => t.Key == key).Title ?? key;

    public static string NormalizeThreat(string? difficulty)
    {
        var d = (difficulty ?? "").Trim().ToLowerInvariant();
        return d switch
        {
            "trivial" or "тривиальная" or "разминка" => "trivial",
            "low" or "easy" or "лёгкая" or "легкая" => "low",
            "severe" or "hard" or "тяжёлая" or "тяжелая" or "сложная" => "severe",
            "extreme" or "deadly" or "смертельная" => "extreme",
            _ => "moderate",
        };
    }

    /// <summary>
    /// Монте-Карло боя: группа бьёт того врага, которого быстрее добить, враги — случайную цель.
    /// Детерминированный seed — одинаковые составы дают одинаковую оценку.
    /// </summary>
    public static Outcome Simulate(IReadOnlyList<Fighter> party, IReadOnlyList<Fighter> enemies, int trials = 400, int seed = 12345)
    {
        if (party.Count == 0 || enemies.Count == 0)
        {
            return new Outcome(0, 0, 0, 0);
        }

        var rng = new Random(seed);
        double roundsSum = 0, lossSum = 0;
        int downs = 0, wipes = 0;
        var totalHp = party.Sum(p => Math.Max(1, p.Hp));
        var pcHp = new double[party.Count];
        var enHp = new double[enemies.Count];

        for (var t = 0; t < trials; t++)
        {
            for (var i = 0; i < party.Count; i++) pcHp[i] = Math.Max(1, party[i].Hp);
            for (var i = 0; i < enemies.Count; i++) enHp[i] = Math.Max(1, enemies[i].Hp);
            var round = 0;
            var anyDown = false;
            while (round < 30)
            {
                round++;
                for (var i = 0; i < party.Count; i++)
                {
                    if (pcHp[i] <= 0) continue;
                    var p = party[i];
                    for (var a = 0; a < p.Attacks; a++)
                    {
                        var target = -1;
                        for (var j = 0; j < enemies.Count; j++)
                        {
                            if (enHp[j] > 0 && (target < 0 || enHp[j] < enHp[target])) target = j;
                        }

                        if (target < 0) break;
                        enHp[target] -= Swing(rng, p, enemies[target].Ac) * p.Boost;
                    }
                }

                if (enHp.All(h => h <= 0)) break;

                for (var j = 0; j < enemies.Count; j++)
                {
                    if (enHp[j] <= 0) continue;
                    var e = enemies[j];
                    for (var a = 0; a < e.Attacks; a++)
                    {
                        var alive = Enumerable.Range(0, party.Count).Where(i => pcHp[i] > 0).ToList();
                        if (alive.Count == 0) break;
                        var i = alive[rng.Next(alive.Count)];
                        pcHp[i] -= Swing(rng, e, party[i].Ac);
                        if (pcHp[i] <= 0) anyDown = true;
                    }
                }

                if (pcHp.All(h => h <= 0)) break;
            }

            roundsSum += round;
            lossSum += Enumerable.Range(0, party.Count).Sum(i => Math.Max(1, party[i].Hp) - Math.Max(0, pcHp[i])) / totalHp;
            if (anyDown) downs++;
            if (pcHp.All(h => h <= 0)) wipes++;
        }

        return new Outcome(roundsSum / trials, lossSum / trials, downs / (double)trials, wipes / (double)trials);
    }

    private static double Swing(Random rng, Fighter f, int ac)
    {
        var d20 = rng.Next(1, 21);
        if (d20 == 1 || (d20 != 20 && d20 + f.Attack < ac))
        {
            return 0;
        }

        var dice = f.Dice * (d20 == 20 ? 2 : 1);
        var dmg = f.Bonus;
        for (var k = 0; k < dice; k++) dmg += rng.Next(1, f.Die + 1);
        return Math.Max(1, dmg);
    }

    // ===== Опыт =====

    /// <summary>Опыт за встречу по угрозе для стандартной группы (как у PF2e: всегда «как для группы из четырёх»).</summary>
    public static int XpForLoss(double loss)
    {
        (double X, double Y)[] curve = { (0, 5), (0.06, 30), (0.16, 60), (0.30, 80), (0.50, 120), (0.72, 160), (1.0, 200) };
        if (loss <= 0) return 5;
        for (var i = 1; i < curve.Length; i++)
        {
            if (loss <= curve[i].X)
            {
                var (x0, y0) = curve[i - 1];
                var (x1, y1) = curve[i];
                return (int)Math.Round(y0 + (y1 - y0) * (loss - x0) / (x1 - x0));
            }
        }

        return 200;
    }

    /// <summary>Опыт за встречу с этими противниками для группы из partySize героев уровня partyLevel.</summary>
    public static int EncounterXp(int partyLevel, int partySize, IReadOnlyList<Fighter> enemies)
    {
        var std = Enumerable.Range(0, Math.Max(1, partySize)).Select(_ => StandardPc(partyLevel)).ToList();
        return XpForLoss(Simulate(std, enemies, 300, 777).Loss);
    }

    /// <summary>Опыт за задания и открытия (не бои).</summary>
    public static int QuestXp(string? size) => (size ?? "").Trim().ToLowerInvariant() switch
    {
        "minor" or "малое" => 20,
        "major" or "крупное" => 120,
        "arc_beat" or "этап" => 150,
        "arc" or "арка" => 300,
        _ => 60,
    };

    /// <summary>Справка для промпта: как считается прогрессия.</summary>
    public static string LevelTableText(params int[] levels)
    {
        var rows = levels.Select(l =>
        {
            var m = Monster(l);
            return Lang.T($"ур.{l}: герой ~{StdPcHp(l)} ХП, КБ {StdPcAc(l)}, атака {StdPcAttack(l):+#}, удар {PowerDice(l)}d8+{StdStatMod(l)}; " +
                   $"рядовой враг ~{m.Hp} ХП, КБ {m.Ac}, {m.AttackText(null)}",
                   $"lvl {l}: hero ~{StdPcHp(l)} HP, AC {StdPcAc(l)}, attack {StdPcAttack(l):+#}, hit {PowerDice(l)}d8+{StdStatMod(l)}; " +
                   $"rank-and-file enemy ~{m.Hp} HP, AC {m.Ac}, {m.AttackText(null)}");
        });
        return string.Join("; ", rows);
    }
}
