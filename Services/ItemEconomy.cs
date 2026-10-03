namespace RPG_Harness.Services;

/// <summary>Категория товара: по ней торговцы решают, что покупать и продавать.</summary>
public sealed record TradeCategory(string Key, string Title, bool Consumable, bool Gear);

/// <summary>Тип торговца: что продаёт (Main), что скупает дёшево (Also), наценка и доля скупки.</summary>
public sealed record MerchantKind(
    string Key, string Title, string[] Main, string[] Also, double Markup, double BuyRate, double OffRate);

/// <summary>
/// Экономика предметов: базовые цены каталога, категории товаров, редкость, типы торговцев,
/// генерация ассортимента лавок и расчёт цен сделки.
/// </summary>
public sealed class ItemEconomy
{
    private readonly ItemCatalog _catalog;
    private readonly Dictionary<string, (string Cat, int Price)> _items = new(StringComparer.OrdinalIgnoreCase);

    public ItemEconomy(ItemCatalog catalog)
    {
        _catalog = catalog;
        foreach (var line in PriceTable.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var sep = line.IndexOf(':');
            var cat = line[..sep].Trim();
            foreach (var entry in line[(sep + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = entry.IndexOf('=');
                _items[entry[..eq]] = (cat, int.Parse(entry[(eq + 1)..]));
            }
        }

        // Киберпанковый каталог несёт цену и категорию торговли прямо в JSON.
        foreach (var def in catalog.Everything.Where(d => d.Trade is { Length: > 0 }))
        {
            _items[def.Id] = (def.Trade!, def.Price);
        }
    }

    // ===== Справочники =====

    /// <summary>Категории текущего сеттинга: ключи общие (логика торговли одна), названия свои.</summary>
    public static TradeCategory[] Categories => Lang.IsEn
        ? Genre.Pick(FantasyCategoriesEn, CyberCategoriesEn, ModernCategoriesEn)
        : Genre.Pick(FantasyCategories, CyberCategories, ModernCategories);

    private static readonly TradeCategory[] ModernCategoriesEn =
    {
        new("blades", "Knives and blades", false, true), new("weapons", "Blunt and improvised weapons", false, true),
        new("polearms", "Heavy weapons", false, true), new("ranged", "Firearms", false, true), new("shields", "Shields", false, true),
        new("heavy_armor", "Body armor and helmets", false, true), new("light_armor", "Tactical clothing", false, true),
        new("clothing", "Clothing", false, true), new("magic", "Tech and electronics", false, true), new("jewelry", "Watches and jewelry", false, true),
        new("gems", "Valuables", false, false), new("potions", "Medicine", true, false), new("scrolls", "Grenades, documents and media", true, false),
        new("books", "Notes and handbooks", false, false), new("food", "Food and drinks", true, false), new("tools", "Tools and gadgets", true, false),
        new("curios", "Odds and ends", false, false), new("materials", "Parts and materials", true, false),
    };

    private static readonly TradeCategory[] CyberCategoriesEn =
    {
        new("blades", "Blades", false, true), new("weapons", "Blunt weapons", false, true), new("polearms", "Heavy weapons", false, true),
        new("ranged", "Firearms", false, true), new("shields", "Shields", false, true), new("heavy_armor", "Tactical armor", false, true),
        new("light_armor", "Jackets and light armor", false, true), new("clothing", "Clothing", false, true), new("magic", "Cyberdecks and software", false, true),
        new("jewelry", "Implants", false, true), new("gems", "Valuables", false, false), new("potions", "Stims and medicine", true, false),
        new("scrolls", "Grenades and chips", true, false), new("books", "Databases and skill chips", false, false), new("food", "Food and drinks", true, false),
        new("tools", "Gadgets and tools", true, false), new("curios", "Curiosities", false, false), new("materials", "Parts and materials", true, false),
    };

    private static readonly TradeCategory[] FantasyCategoriesEn =
    {
        new("blades", "Blades", false, true), new("weapons", "Axes, maces and other weapons", false, true),
        new("polearms", "Polearms and staves", false, true), new("ranged", "Bows, crossbows, ammunition", false, true), new("shields", "Shields", false, true),
        new("heavy_armor", "Plate and mail", false, true), new("light_armor", "Leather armor and gear", false, true),
        new("clothing", "Clothing, robes, cloaks", false, true), new("magic", "Magic items", false, true), new("jewelry", "Rings and amulets", false, true),
        new("gems", "Gems and valuables", false, false), new("potions", "Potions and elixirs", true, false), new("scrolls", "Scrolls and maps", true, false),
        new("books", "Books", false, false), new("food", "Food and drinks", true, false), new("tools", "Tools and camping gear", true, false),
        new("curios", "Curiosities and trophies", false, false), new("materials", "Materials and monster trophies", true, false),
    };

    private static readonly TradeCategory[] ModernCategories =
    {
        new("blades", "Ножи и клинки", false, true),
        new("weapons", "Ударное и подручное", false, true),
        new("polearms", "Тяжёлое оружие", false, true),
        new("ranged", "Огнестрел", false, true),
        new("shields", "Щиты", false, true),
        new("heavy_armor", "Бронежилеты и каски", false, true),
        new("light_armor", "Тактическая одежда", false, true),
        new("clothing", "Одежда", false, true),
        new("magic", "Техника и электроника", false, true),
        new("jewelry", "Часы и украшения", false, true),
        new("gems", "Ценности", false, false),
        new("potions", "Лекарства", true, false),
        new("scrolls", "Гранаты, документы и носители", true, false),
        new("books", "Записи и справочники", false, false),
        new("food", "Еда и напитки", true, false),
        new("tools", "Инструменты и гаджеты", true, false),
        new("curios", "Мелочи", false, false),
        new("materials", "Запчасти и материалы", true, false),
    };

    private static readonly TradeCategory[] CyberCategories =
    {
        new("blades", "Клинки", false, true),
        new("weapons", "Ударное оружие", false, true),
        new("polearms", "Тяжёлое оружие", false, true),
        new("ranged", "Огнестрел", false, true),
        new("shields", "Щиты", false, true),
        new("heavy_armor", "Тактическая броня", false, true),
        new("light_armor", "Куртки и лёгкая броня", false, true),
        new("clothing", "Одежда", false, true),
        new("magic", "Кибердеки и софт", false, true),
        new("jewelry", "Импланты", false, true),
        new("gems", "Ценности", false, false),
        new("potions", "Стимуляторы и медицина", true, false),
        new("scrolls", "Гранаты и чипы", true, false),
        new("books", "Базы данных и обучающие чипы", false, false),
        new("food", "Еда и напитки", true, false),
        new("tools", "Гаджеты и инструменты", true, false),
        new("curios", "Диковины", false, false),
        new("materials", "Запчасти и материалы", true, false),
    };

    private static readonly TradeCategory[] FantasyCategories =
    {
        new("blades", "Клинки", false, true),
        new("weapons", "Топоры, булавы и прочее оружие", false, true),
        new("polearms", "Древковое оружие и посохи", false, true),
        new("ranged", "Луки, арбалеты, снаряды", false, true),
        new("shields", "Щиты", false, true),
        new("heavy_armor", "Латы и кольчуги", false, true),
        new("light_armor", "Кожаная броня и снаряжение", false, true),
        new("clothing", "Одежда, мантии, плащи", false, true),
        new("magic", "Магические предметы", false, true),
        new("jewelry", "Кольца и амулеты", false, true),
        new("gems", "Самоцветы и ценности", false, false),
        new("potions", "Зелья и эликсиры", true, false),
        new("scrolls", "Свитки и карты", true, false),
        new("books", "Книги", false, false),
        new("food", "Еда и напитки", true, false),
        new("tools", "Инструменты и походное снаряжение", true, false),
        new("curios", "Диковины и трофеи", false, false),
        new("materials", "Материалы и трофеи с тварей", true, false),
    };

    /// <summary>Типы торговцев текущего сеттинга.</summary>
    public static MerchantKind[] Merchants => Lang.IsEn
        ? Genre.Pick(FantasyMerchants, CyberMerchants, ModernMerchants).Select(m => m with { Title = MerchantTitleEn(m.Key, m.Title) }).ToArray()
        : Genre.Pick(FantasyMerchants, CyberMerchants, ModernMerchants);

    /// <summary>Английские названия типов торговцев (механика у них та же, что у русских записей).</summary>
    private static string MerchantTitleEn(string key, string fallback) => (Genre.Current, key) switch
    {
        (_, "fence") => "Fence",
        (Genre.Modern, "gunshop") => "Gun store", (Genre.Modern, "blackmarket") => "Underground arms dealer",
        (Genre.Modern, "outfitter") => "Gear and clothing store", (Genre.Modern, "pharmacy") => "Pharmacy",
        (Genre.Modern, "hardware") => "Hardware store", (Genre.Modern, "electronics") => "Electronics store",
        (Genre.Modern, "pawnshop") => "Pawnshop", (Genre.Modern, "occult") => "Occult shop", (Genre.Modern, "autoparts") => "Auto parts",
        (Genre.Modern, "general") => "Grocery store", (Genre.Modern, "bar") => "Bar",
        (Genre.Cyberpunk, "gunsmith") => "Gunsmith", (Genre.Cyberpunk, "outfitter") => "Gear shop", (Genre.Cyberpunk, "ripperdoc") => "Ripperdoc",
        (Genre.Cyberpunk, "netdealer") => "Netrunner dealer", (Genre.Cyberpunk, "pharmacy") => "Pharmacy", (Genre.Cyberpunk, "techshop") => "Electronics shop",
        (Genre.Cyberpunk, "boutique") => "Valuables buyer", (Genre.Cyberpunk, "general") => "Street stall", (Genre.Cyberpunk, "bar") => "Bar",
        (_, "blacksmith") => "Blacksmith", (_, "bowyer") => "Bowyer", (_, "tailor") => "Tailor and leatherworker", (_, "alchemist") => "Alchemist",
        (_, "mage") => "Enchanter's shop", (_, "jeweler") => "Jeweler", (_, "general") => "Shopkeeper", (_, "innkeeper") => "Innkeeper",
        (_, "scribe") => "Bookseller and cartographer", (_, "temple") => "Temple shop",
        _ => fallback,
    };

    /// <summary>Все ключи категорий (скупщик берёт всё). Объявлен раньше таблиц торговцев: статические поля инициализируются по порядку.</summary>
    private static readonly string[] AllCategoryKeys =
    {
        "blades", "weapons", "polearms", "ranged", "shields", "heavy_armor", "light_armor", "clothing", "magic", "jewelry",
        "gems", "potions", "scrolls", "books", "food", "tools", "curios", "materials",
    };

    private static readonly MerchantKind[] ModernMerchants =
    {
        new("gunshop", "Оружейный магазин",
            new[] { "blades", "ranged" },
            new[] { "light_armor", "tools", "materials" }, 1.0, 0.5, 0.15),
        new("blackmarket", "Подпольный торговец оружием",
            new[] { "ranged", "polearms", "scrolls", "heavy_armor", "shields" },
            new[] { "blades", "weapons", "materials", "magic" }, 1.3, 0.4, 0.2),
        new("outfitter", "Магазин экипировки и одежды",
            new[] { "heavy_armor", "light_armor", "clothing" },
            new[] { "tools", "jewelry" }, 1.0, 0.5, 0.15),
        new("pharmacy", "Аптека",
            new[] { "potions" },
            new[] { "food" }, 1.1, 0.45, 0.15),
        new("hardware", "Хозяйственный магазин",
            new[] { "tools", "materials", "weapons" },
            new[] { "blades", "food" }, 1.0, 0.45, 0.15),
        new("electronics", "Магазин электроники",
            new[] { "magic", "tools", "books" },
            new[] { "materials", "scrolls" }, 1.1, 0.45, 0.2),
        new("pawnshop", "Ломбард",
            new[] { "gems", "jewelry", "curios" },
            new[] { "ranged", "blades", "magic", "clothing", "tools" }, 1.2, 0.45, 0.2),
        new("occult", "Оккультная лавка",
            new[] { "books", "curios", "jewelry" },
            new[] { "potions", "magic", "materials" }, 1.2, 0.45, 0.15),
        new("autoparts", "Автозапчасти",
            new[] { "materials", "tools" },
            new[] { "weapons" }, 1.0, 0.5, 0.15),
        new("general", "Продуктовый магазин",
            new[] { "food", "tools" },
            new[] { "curios", "potions" }, 1.1, 0.4, 0.15),
        new("bar", "Бар",
            new[] { "food" },
            new[] { "curios", "gems" }, 1.2, 0.4, 0.1),
        new("fence", "Скупщик краденого",
            AllCategoryKeys,
            Array.Empty<string>(), 1.3, 0.3, 0),
    };

    private static readonly MerchantKind[] CyberMerchants =
    {
        new("gunsmith", "Оружейник",
            new[] { "blades", "weapons", "polearms", "ranged", "shields" },
            new[] { "heavy_armor", "light_armor", "scrolls", "materials" }, 1.0, 0.5, 0.15),
        new("outfitter", "Магазин экипировки",
            new[] { "heavy_armor", "light_armor", "clothing" },
            new[] { "tools", "shields", "curios" }, 1.0, 0.5, 0.15),
        new("ripperdoc", "Риппердок",
            new[] { "jewelry", "potions" },
            new[] { "materials", "books", "gems" }, 1.15, 0.5, 0.2),
        new("netdealer", "Нетраннер-барыга",
            new[] { "magic", "scrolls", "books" },
            new[] { "tools", "materials", "gems" }, 1.2, 0.45, 0.2),
        new("pharmacy", "Аптека",
            new[] { "potions", "food" },
            new[] { "materials" }, 1.1, 0.45, 0.15),
        new("techshop", "Магазин электроники",
            new[] { "tools", "materials", "books" },
            new[] { "magic", "scrolls", "curios" }, 1.1, 0.45, 0.2),
        new("boutique", "Скупка ценностей",
            new[] { "gems", "curios" },
            new[] { "clothing", "jewelry" }, 1.15, 0.6, 0.2),
        new("general", "Уличный ларёк",
            new[] { "food", "tools", "potions", "clothing", "light_armor" },
            new[] { "blades", "weapons", "ranged", "scrolls", "curios", "materials", "gems" }, 1.1, 0.4, 0.2),
        new("bar", "Бар",
            new[] { "food" },
            new[] { "curios", "gems" }, 1.2, 0.4, 0.1),
        new("fence", "Скупщик краденого",
            AllCategoryKeys,
            Array.Empty<string>(), 1.3, 0.3, 0),
    };

    private static readonly MerchantKind[] FantasyMerchants =
    {
        new("blacksmith", "Кузнец",
            new[] { "blades", "weapons", "polearms", "heavy_armor", "shields" },
            new[] { "ranged", "light_armor", "tools", "materials" }, 1.0, 0.5, 0.15),
        new("bowyer", "Лучник-оружейник",
            new[] { "ranged", "light_armor" },
            new[] { "blades", "polearms", "tools", "clothing" }, 1.0, 0.5, 0.15),
        new("tailor", "Портной и кожевник",
            new[] { "clothing", "light_armor" },
            new[] { "jewelry", "tools", "materials" }, 1.0, 0.5, 0.15),
        new("alchemist", "Алхимик",
            new[] { "potions", "curios", "materials" },
            new[] { "gems", "food", "books", "magic" }, 1.1, 0.5, 0.2),
        new("mage", "Лавка чародея",
            new[] { "magic", "scrolls", "books", "jewelry" },
            new[] { "potions", "gems", "curios", "materials" }, 1.2, 0.45, 0.2),
        new("jeweler", "Ювелир",
            new[] { "jewelry", "gems" },
            new[] { "magic", "curios" }, 1.15, 0.6, 0.2),
        new("general", "Лавочник",
            new[] { "food", "tools", "potions", "light_armor", "clothing" },
            new[] { "blades", "weapons", "polearms", "ranged", "shields", "scrolls", "books", "gems", "curios", "materials" }, 1.1, 0.4, 0.2),
        new("innkeeper", "Трактирщик",
            new[] { "food" },
            new[] { "tools", "gems" }, 1.2, 0.4, 0.1),
        new("scribe", "Книжник и картограф",
            new[] { "books", "scrolls" },
            new[] { "tools", "magic", "curios" }, 1.1, 0.5, 0.15),
        new("temple", "Храмовая лавка",
            new[] { "potions", "scrolls", "jewelry" },
            new[] { "books", "curios", "gems" }, 1.0, 0.4, 0.15),
        new("fence", "Скупщик",
            AllCategoryKeys,
            Array.Empty<string>(), 1.3, 0.3, 0),
    };

    /// <summary>Порядок редкости и множитель цены.</summary>
    public static (string Key, string Title, double Mult)[] Rarities => Lang.IsEn
        ? Genre.Pick(FantasyRaritiesEn, CyberRaritiesEn, ModernRaritiesEn)
        : Genre.Pick(FantasyRarities, CyberRarities, ModernRarities);

    private static readonly (string Key, string Title, double Mult)[] ModernRaritiesEn =
        { ("common", "common", 1), ("uncommon", "improved", 2.5), ("rare", "professional", 6), ("epic", "exclusive", 15), ("legendary", "legendary", 50) };

    private static readonly (string Key, string Title, double Mult)[] FantasyRaritiesEn =
        { ("common", "common", 1), ("uncommon", "uncommon", 2.5), ("rare", "rare", 6), ("epic", "epic", 15), ("legendary", "legendary", 50) };

    private static readonly (string Key, string Title, double Mult)[] CyberRaritiesEn =
        { ("common", "stock", 1), ("uncommon", "modded", 2.5), ("rare", "military", 6), ("epic", "prototype", 15), ("legendary", "iconic", 50) };

    /// <summary>В современности ступени — по происхождению и классу вещи.</summary>
    private static readonly (string Key, string Title, double Mult)[] ModernRarities =
    {
        ("common", "обычный", 1),
        ("uncommon", "улучшенный", 2.5),
        ("rare", "профессиональный", 6),
        ("epic", "эксклюзивный", 15),
        ("legendary", "легендарный", 50),
    };

    private static readonly (string Key, string Title, double Mult)[] FantasyRarities =
    {
        ("common", "обычный", 1),
        ("uncommon", "добротный", 2.5),
        ("rare", "редкий", 6),
        ("epic", "эпический", 15),
        ("legendary", "легендарный", 50),
    };

    /// <summary>В киберпанке те же ступени называются по происхождению вещи.</summary>
    private static readonly (string Key, string Title, double Mult)[] CyberRarities =
    {
        ("common", "серийный", 1),
        ("uncommon", "модифицированный", 2.5),
        ("rare", "военный", 6),
        ("epic", "прототип", 15),
        ("legendary", "культовый", 50),
    };

    /// <summary>Предметы, которые никогда не попадают в обычный ассортимент (только сюжетные).</summary>
    internal static readonly HashSet<string> StoryOnly = new(StringComparer.OrdinalIgnoreCase)
    {
        "dragon_armour", "demon_blade", "elder_staff", "orb", "lightning_rod", "talisman_dragon", "book_dead", "rune", "avatar",
        "coins", "gold_pile",
    };

    /// <summary>Что делает зелье каждого цвета — справка для модели (иконку выбирают по функции, а не по цвету).</summary>
    public static string PotionHints() => Genre.IsModern
        ? Lang.T("Лекарства по функции: ", "Medicine by function: ") + string.Join(", ", Potions.Where(p => p.Key.StartsWith("md_")).Select(p => $"{p.Key} — {p.Value.Note.ToLowerInvariant().TrimEnd('.')}")) + "."
        : Genre.IsCyber
        ? Lang.T("Стимы по функции: ", "Stims by function: ") + string.Join(", ", Potions.Where(p => p.Key.StartsWith("cy_")).Select(p => $"{p.Key} — {p.Value.Note.ToLowerInvariant().TrimEnd('.')}")) + "."
        : Lang.T("Зелья по функции: ", "Potions by function: ") + string.Join(", ", Potions.Where(p => !p.Key.StartsWith("cy_") && !p.Key.StartsWith("md_")).Select(p => $"{p.Key} — {p.Value.Name.ToLowerInvariant()}")) + ".";

    /// <summary>Названия и эффекты расходников на языке интерфейса.</summary>
    private static Dictionary<string, (string Name, string Note)> Potions => Lang.IsEn ? PotionIdentityEn : PotionIdentity;

    private static readonly Dictionary<string, (string Name, string Note)> PotionIdentityEn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["potion_red"]=("Minor healing potion","Restores 1d6+2 HP."), ["potion_cyan"]=("Minor mana potion","Restores 1d6+2 mana."),
        ["potion_emerald"]=("Antidote","Removes ordinary poison."), ["potion_amber"]=("Potion of strength","For one scene grants advantage on Strength checks."),
        ["potion_smoky"]=("Potion of invisibility","Makes you invisible for up to a minute or until you attack."), ["potion_blue"]=("Potion of protection","Grants 6 points of energy shield."),
        ["potion_golden"]=("Greater elixir of restoration","Restores 3d6 HP and 2d6 mana."), ["potion_pink"]=("Elixir of charm","For one scene boosts Charisma."),
        ["potion_black"]=("Strong poison","Applied to a weapon; causes poisoning."), ["potion_white"]=("Potion of cleansing","Removes one ordinary debuff."),
        ["potion_bubbly"]=("Potion of speed","Hastes the hero for 3 rounds."), ["potion_murky"]=("Cheap healing brew","Restores 1d4 HP."),
        ["potion_magenta"]=("Potion of arcane might","Empowers the next spell."), ["phial"]=("Alchemical essence","A reagent for potions and enchantments."),
        ["cy_medstim"]=("Medstim","Restores 1d6+2 HP."), ["cy_ram_stim"]=("RAM stim","Restores 1d6+2 RAM."),
        ["cy_antitox"]=("Antitoxin","Removes an ordinary toxin."), ["cy_combat_stim"]=("Combat stim","For one scene grants advantage on Strength checks."),
        ["cy_stealth_stim"]=("Stealth stim","Camouflage for up to a minute or until you attack."), ["cy_shield_stim"]=("Armor gel injection","Grants 6 points of shield."),
        ["cy_reflex_stim"]=("Reflex stim","Hastes you for 3 rounds."), ["cy_neuro_stim"]=("Neuro booster","Empowers the next RAM skill."),
        ["cy_cleanser"]=("System cleanser","Removes one ordinary debuff."), ["cy_toxin"]=("Neurotoxin","Applied to a blade; causes poisoning."),
        ["cy_cheap_stim"]=("Cheap stim","Restores 1d4 HP."), ["cy_medkit"]=("First aid kit","Restores 3d6 HP outside combat."),
        ["cy_trauma_kit"]=("Resuscitation kit","Gets a fallen ally up with 1d6 HP."),
        ["md_bandage"]=("Bandages and painkillers","Restores 1d6+2 HP, stops bleeding."),
        ["md_medkit"]=("First aid kit","Restores 3d6 HP outside combat."),
        ["md_army_medkit"]=("Army first aid kit","Gets a fallen ally up with 1d6 HP."),
        ["md_painkillers"]=("Painkillers","Removes one pain or concussion effect."),
        ["md_energy"]=("Energy drink","Restores 1d6+2 focus."),
        ["md_adrenaline"]=("Adrenaline syringe","+2 to attack and damage for 3 rounds."),
        ["md_antidote"]=("Antidote","Removes poisoning."),
        ["md_sedative"]=("Sedative","Removes panic, advantage on WIS saving throws until the end of the scene."),
    };

    private static readonly Dictionary<string, (string Name, string Note)> PotionIdentity = new(StringComparer.OrdinalIgnoreCase)
    {
        ["potion_red"]=("Малое зелье лечения","Восстанавливает 1d6+2 HP."), ["potion_cyan"]=("Малое зелье маны","Восстанавливает 1d6+2 маны."),
        ["potion_emerald"]=("Противоядие","Снимает обычный яд."), ["potion_amber"]=("Зелье силы","На одну сцену даёт преимущество проверкам Силы."),
        ["potion_smoky"]=("Зелье невидимости","Делает невидимым до минуты или до атаки."), ["potion_blue"]=("Зелье защиты","Даёт 6 очков энергетического щита."),
        ["potion_golden"]=("Большой эликсир восстановления","Восстанавливает 3d6 HP и 2d6 маны."), ["potion_pink"]=("Эликсир обаяния","На одну сцену усиливает Харизму."),
        ["potion_black"]=("Сильный яд","Наносится на оружие; вызывает отравление."), ["potion_white"]=("Зелье очищения","Снимает один обычный дебаф."),
        ["potion_bubbly"]=("Зелье скорости","Ускоряет героя на 3 раунда."), ["potion_murky"]=("Дешёвый лечебный отвар","Восстанавливает 1d4 HP."),
        ["potion_magenta"]=("Зелье магической мощи","Усиливает следующее заклинание."), ["phial"]=("Алхимическая эссенция","Реагент для зелий и чар."),
        // Киберпанк: названия уже функциональные, здесь — эффекты для заметки.
        ["cy_medstim"]=("Медстим","Восстанавливает 1d6+2 HP."), ["cy_ram_stim"]=("Стим ОЗУ","Восстанавливает 1d6+2 ОЗУ."),
        ["cy_antitox"]=("Антитоксин","Снимает обычный токсин."), ["cy_combat_stim"]=("Боевой стим","На одну сцену даёт преимущество проверкам Силы."),
        ["cy_stealth_stim"]=("Стим-невидимка","Камуфляж до минуты или до атаки."), ["cy_shield_stim"]=("Инъекция бронегеля","Даёт 6 очков щита."),
        ["cy_reflex_stim"]=("Стим рефлексов","Ускоряет на 3 раунда."), ["cy_neuro_stim"]=("Нейроусилитель","Усиливает следующий навык за ОЗУ."),
        ["cy_cleanser"]=("Очиститель систем","Снимает один обычный дебаф."), ["cy_toxin"]=("Нейротоксин","Наносится на клинок; вызывает отравление."),
        ["cy_cheap_stim"]=("Дешёвый стим","Восстанавливает 1d4 HP."), ["cy_medkit"]=("Аптечка","Восстанавливает 3d6 HP вне боя."),
        ["cy_trauma_kit"]=("Реанимационный набор","Поднимает упавшего союзника с 1d6 HP."),
        // Современность.
        ["md_bandage"]=("Бинты и обезболивающее","Восстанавливает 1d6+2 HP, останавливает кровотечение."),
        ["md_medkit"]=("Аптечка","Восстанавливает 3d6 HP вне боя."),
        ["md_army_medkit"]=("Армейская аптечка","Поднимает упавшего союзника с 1d6 HP."),
        ["md_painkillers"]=("Обезболивающее","Снимает один эффект боли или контузии."),
        ["md_energy"]=("Энергетик","Восстанавливает 1d6+2 концентрации."),
        ["md_adrenaline"]=("Шприц адреналина","На 3 раунда +2 к атаке и урону."),
        ["md_antidote"]=("Противоядие","Снимает отравление."),
        ["md_sedative"]=("Успокоительное","Снимает панику, преимущество на спасброски МДР до конца сцены."),
    };

    /// <summary>Стакающиеся предметы и типичный размер стопки в лавке.</summary>
    private static readonly Dictionary<string, (int Min, int Max)> Stacks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["potions"] = (1, 4), ["food"] = (2, 6), ["scrolls"] = (1, 3),
        ["arrows"] = (1, 3), ["bolts"] = (1, 3), ["darts"] = (3, 10), ["javelin"] = (2, 4),
        ["torch"] = (2, 6), ["coins"] = (1, 1), ["gem"] = (1, 2),
    };

    /// <summary>Категория и базовая цена (в золоте) каждого предмета каталога.</summary>
    private const string PriceTable = """
        blades: dagger=2 athame=12 short_sword=10 sword=15 long_sword=18 rapier=25 scimitar=22 falchion=20 demon_blade=600 greatsword=50
        weapons: hand_axe=5 axe=10 war_axe=20 club=1 mace=5 morningstar=15 flail=10 hammer=12 whip=2 battle_axe=30 executioner_axe=45 great_mace=35
        polearms: staff=5 quarterstaff=2 spear=2 trident=12 halberd=20 glaive=20 scythe=8 javelin=1
        ranged: bow=25 shortbow=25 longbow=50 crossbow=40 sling=1 hand_cannon=150 arrows=2 bolts=2 darts=1 boomerang=3 net=1
        shields: buckler=5 shield=10 kite_shield=20 tower_shield=35
        heavy_armor: ring_mail=30 chain_mail=75 scale_mail=50 plate=400 dragon_armour=2000 helmet=10 great_helm=25 horned_helm=18 gauntlets=15 heavy_boots=12
        light_armor: body=10 leather_armour=10 animal_skin=5 leather_helm=3 gloves=2 boots=2 belt=2 leather_cloak=6 archer_hat=4
        clothing: robe=2 mage_robe=45 cloak=3 fine_cloak=25 scarf=2 hat=1 wizard_hat=15 fine_gloves=12 fine_boots=15
        magic: wand=60 rod=90 mage_staff=80 elder_staff=300 focus_orb=60 orb=200 glass_orb=30 lightning_rod=180 talisman_dragon=250 talisman_spider=120 rune=100
        jewelry: amulet=40 amulet_ruby=120 amulet_silver=60 amulet_bone=25 amulet_skull=80 amulet_sun=150 amulet_scarab=90 ring=30 ring2=40 ring_gold=60 ring_silver=35 ring_iron=10 ring_ruby=120 ring_emerald=110 ring_snake=70 ring_eye=150
        gems: gem=25 gem_red=50 gem_green=40 gem_blue=50 gem_violet=60 gem_yellow=45 coins=10 gold_pile=50 idol=30
        potions: potion_red=6 potion_amber=14 potion_cyan=7 potion_emerald=8 potion_smoky=28 potion_blue=10 potion_golden=35 potion_pink=12 potion_black=18 potion_white=12 potion_bubbly=15 potion_murky=3 potion_magenta=18 phial=5
        scrolls: scroll=15 scroll_red=30 scroll_blue=25 scroll_green=25 parchment=1 map=20
        books: book=15 book_red=40 book_green=40 book_blue=40 book_gold=120 book_dead=200 manual=60
        food: bread=1 meat=1 jerky=1 sausage=1 cheese=1 apple=1 pear=1 orange=1 grapes=1 strawberry=1 honeycomb=2 bottle=2
        tools: key=1 lamp=5 lantern=8 sack=1 horn=5 tambourine=3 fan=4 mirror=10 box=3 vane=5 gizmo=25 torch=1 pickaxe=2 lens=12 compass=20 drum=6 voucher=5
        curios: skull_lantern=40 stone=1 bone=1 skull=2
        blades: sword_singing=25 sword_power=25 katana=30 sword_blood=20 sword_flame=25 sword_arc=25 dagger_vampire=8 dagger_precise=8 cutlass=20 scimitar_bloom=25 sword_leech=20 sabre_crimson=22 double_sword=40 quickblade=60 triple_sword=120 blessed_blade=30 long_sword_steel=60 dagger_steel=15
        weapons: axe_frost=25 mace_holy=20 maul_skull=40 axe_holy=45 axe_demon=45 maul_dark=40 sceptre_torment=60 broad_axe=25 dire_flail=45 eveningstar=40 giant_club=20 spiked_club=35 demon_whip=20 sacred_scourge=20 mace_flanged=20 whip_spell=30
        polearms: lance_wyrm=40 spear_crystal=35 scythe_curse=30 lance_order=35 lance_force=35 bardiche=35 lajatang=60 partisan=30 demon_trident=30 trishula=35
        ranged: bow_storm=60 crossbow_sniper=60 orcbow=40 blowgun=10 silver_arrows=10 sling_bullets=1 triple_crossbow=150
        magic: staff_venom=90 staff_elements=100 staff_fire=90 wand_winter=70 staff_battle=90 staff_crook=80 orb_chaos=70 staff_bone=90 staff_gold=160 staff_blood=140 staff_sun=150 staff_ember=120 staff_orb=130 staff_spiral=110 staff_arcane=180 wand_bone=50 wand_fire=90 wand_void=110 wand_frost=90 wand_storm=100 wand_charm=100 rod_blood=150 rod_royal=220 rod_shadow=180 crystal_ball=80 phantom_mirror=150
        shields: shield_storm=30 shield_gong=20 shield_heraldic=30 tower_shield_dark=50 buckler_bronze=12
        heavy_armor: mail_salamander=90 plate_orange=400 armor_justicar=400 plate_crystal=450 plate_dark=500 brigandine=90 helm_dragon=25 gauntlets_war=20 helm_ornate=60 helm_plumed=45 helm_barbute=30 gauntlets_ornate=50 crown=60 scale_dragonking=120 armor_bone=80
        light_armor: dragon_storm_armour=300 dragon_ice_armour=300 dragon_shadow_armour=300 dragon_gold_armour=350 dragon_acid_armour=280 dragon_swamp_armour=250 dragon_pearl_armour=320 troll_hide=250 boots_seven=20 boots_spider=15 boots_mountain=15 boots_elven=60 hat_bear=10 gloves_power=15
        clothing: cloak_night=30 cloak_starlight=30 cloak_rat=15 cloak_thief=25 hat_council=20 robe_vines=45 slippers=15 robe_teal=40 robe_ornate=120 cloak_grey=8 cloak_royal=80 hat_explorer=10 cap_jester=5
        jewelry: amulet_amethyst=90 amulet_jade=80 amulet_pearl=140 amulet_garnet=100 amulet_gold=150 amulet_platinum=200 amulet_cameo=110 amulet_filigree=130 ring_jade=60 ring_opal=90 ring_coral=50 ring_moonstone=100 ring_tiger=70 ring_diamond=250 ring_wood=8 ring_fire=180 ring_ice=180 ring_blood=160 ring_void=200
        gems: talisman_maw=90 talisman_wolf=60 talisman_frost=110 talisman_medusa=150 talisman_bat=100 talisman_storm=140 talisman_sphinx=160 talisman_fortress=120 talisman_death=180 talisman_blade=110 talisman_quill=70 talisman_hive=80 void_sigil=250 golden_ziggurat=300 rune_abyss=150 rune_elven=120 skull_cursed=60 gem_pale=45 gem_pink=70 gem_cyan=60 gem_aqua=40 gem_peach=55 gem_amber=35
        materials: fang=4 horned_skull=12 scales=15 herbs=3 mushroom=2 ingot=10 fur=5 leather=6 crystals=20
        tools: rope=1 war_horn=30 ornate_flask=12
        food: raw_meat=1 meat_ration=2 ration=2 banana=1 lemon=1 apricot=1
        books: book_purple=80 book_silver=90 book_cloth=20 book_leather=15 book_turquoise=60 book_magenta=70 book_star=100
        scrolls: scroll_purple=35 scroll_yellow=30 scroll_old=20
        curios: rock=1 bones=1
        """;

    // ===== Справки =====

    public static TradeCategory? Category(string? key) => Categories.FirstOrDefault(c => c.Key == key);

    public static MerchantKind Merchant(string? key) =>
        Merchants.FirstOrDefault(m => m.Key.Equals(key ?? "", StringComparison.OrdinalIgnoreCase))
        ?? FantasyMerchants.Concat(CyberMerchants).Concat(ModernMerchants).FirstOrDefault(m => m.Key.Equals(key ?? "", StringComparison.OrdinalIgnoreCase))
        ?? Merchants.First(m => m.Key == "general");

    public static string RarityKey(string? rarity)
    {
        var r = (rarity ?? "").Trim().ToLowerInvariant();
        return r switch
        {
            "" or "обычный" or "обычная" or "серийный" or "серийная" or "stock" => "common",
            "добротный" or "необычный" or "uncommon" or "модифицированный" or "модифицированная" or "улучшенный" or "улучшенная" or "modded" or "improved" => "uncommon",
            "редкий" or "rare" or "военный" or "военная" or "профессиональный" or "профессиональная" or "military" or "professional" => "rare",
            "эпический" or "epic" or "прототип" or "эксклюзивный" or "эксклюзивная" or "prototype" or "exclusive" => "epic",
            "легендарный" or "legendary" or "культовый" or "культовая" or "iconic" => "legendary",
            _ => Rarities.Any(x => x.Key == r) ? r : "common",
        };
    }

    public static string RarityTitle(string? rarity) => Rarities.First(r => r.Key == RarityKey(rarity)).Title;

    private static double RarityMult(string? rarity) => FantasyRarities.First(r => r.Key == RarityKey(rarity)).Mult;

    /// <summary>Категория товара по иконке (неизвестные иконки — «диковины»).</summary>
    public string CategoryOf(string? icon) => icon is not null && _items.TryGetValue(icon, out var e) ? e.Cat : "curios";

    public int BasePrice(string? icon) => icon is not null && _items.TryGetValue(icon, out var e) ? e.Price : 5;

    /// <summary>Стоимость одной штуки: явная цена предмета или база каталога × редкость.</summary>
    public int UnitValue(GridItem item) =>
        item.Value > 0 ? item.Value : Math.Max(1, (int)Math.Round(BasePrice(item.Icon) * RarityMult(item.Rarity)));

    // ===== Цены сделки =====

    /// <summary>Скидка/надбавка от Харизмы героя: ±3% за единицу модификатора (не больше 15%).</summary>
    private static double CharismaFactor(CharacterSheet hero) =>
        Math.Clamp(DndStatNames.Modifier(hero.Cha) * 0.03, -0.15, 0.15);

    public string[] MainOf(MerchantShop shop) => shop.Buys.Count > 0 ? shop.Buys.ToArray() : Merchant(shop.Kind).Main;

    /// <summary>Смежные категории (без своих — если мастер указал одну категорию в обоих списках).</summary>
    public string[] AlsoOf(MerchantShop shop) =>
        (shop.Also.Count > 0 ? shop.Also.ToArray() : Merchant(shop.Kind).Also).Except(MainOf(shop)).ToArray();

    /// <summary>Цена, которую платит герой за штуку товара лавки.</summary>
    public int BuyPrice(MerchantShop shop, GridItem item, CharacterSheet hero) =>
        Math.Max(1, (int)Math.Ceiling(UnitValue(item) * shop.Markup * (1 - CharismaFactor(hero))));

    /// <summary>Сколько торговец даст за штуку (0 — не покупает такое вообще).</summary>
    public int SellPrice(MerchantShop shop, GridItem item, CharacterSheet hero)
    {
        var cat = CategoryOf(item.Icon);
        double rate = MainOf(shop).Contains(cat) ? shop.BuyRate : AlsoOf(shop).Contains(cat) ? shop.OffRate : 0;
        if (rate <= 0)
        {
            return 0;
        }

        var price = UnitValue(item) * rate * (1 + CharismaFactor(hero));
        return Math.Max(1, (int)Math.Floor(price));
    }

    /// <summary>Почему торговец не берёт товар — для подсказки игроку.</summary>
    public string RefuseReason(MerchantShop shop, GridItem item)
    {
        var cat = Category(CategoryOf(item.Icon))?.Title.ToLowerInvariant() ?? Lang.T("такое", "such things");
        return Lang.T($"{Merchant(shop.Kind).Title} не покупает: {cat}.", $"{Merchant(shop.Kind).Title} does not buy: {cat}.");
    }

    // ===== Генерация ассортимента =====

    /// <summary>
    /// Ассортимент по правилам: много расходников и полезной мелочи, экипировки меньше,
    /// добротные вещи реже, редкие — очень редко (и только в городах), легендарных нет вовсе.
    /// </summary>
    public List<GridItem> GenerateStock(MerchantShop shop, Random? rng = null)
    {
        rng ??= Random.Shared;
        var tier = (shop.Tier ?? "town").ToLowerInvariant();
        var (minCount, maxCount, priceCap) = tier switch
        {
            "village" => (6, 9, 60),
            "city" => (13, 20, 600),
            _ => (9, 14, 200),
        };

        var main = MainOf(shop);
        var pool = _catalog.All
            .Where(d => !d.Art && !StoryOnly.Contains(d.Id) && _items.ContainsKey(d.Id))
            .Where(d => main.Contains(CategoryOf(d.Id)) && BasePrice(d.Id) <= priceCap)
            .ToList();

        var result = new List<GridItem>();
        if (pool.Count == 0)
        {
            return result;
        }

        var target = rng.Next(minCount, maxCount + 1);
        for (var attempt = 0; attempt < target * 4 && result.Count < target; attempt++)
        {
            var def = PickWeighted(pool, rng);
            var cat = Category(CategoryOf(def.Id))!;

            // Одинаковые вещи не дублируем — расходникам просто увеличиваем стопку.
            var existing = result.FirstOrDefault(i => i.Icon == def.Id);
            if (existing is not null)
            {
                if (cat.Consumable)
                {
                    existing.Quantity++;
                }

                continue;
            }

            var rarity = cat.Gear ? RollRarity(tier, rng) : "common";
            var item = new GridItem
            {
                Name = def.Name,
                Icon = def.Id,
                W = def.W,
                H = def.H,
                Slot = def.EquipSlot,
                TwoHanded = def.TwoHanded,
                Rarity = rarity,
                Quantity = StackSize(def.Id, cat.Key, rng),
                Note = RarityNote(rarity),
            };

            if (Potions.TryGetValue(def.Id, out var potion)) { item.Name = potion.Name; item.Note = potion.Note; }

            ItemStats.ApplyBase(item);
            ItemStats.RollBonuses(item, rng);

            if (rarity != "common")
            {
                item.Name = $"{def.Name} ({RarityTitle(rarity)})";
            }

            var spot = InventoryOps.FindFree(result, item.W, item.H, null, null, null, MerchantShop.ShopCols, MerchantShop.ShopRows);
            if (spot is null)
            {
                continue;
            }

            (item.Col, item.Row) = spot.Value;
            result.Add(item);
        }

        return result;
    }

    /// <summary>Деньги торговца по размеру поселения.</summary>
    public static int RollGold(string? tier, Random? rng = null)
    {
        rng ??= Random.Shared;
        return (tier ?? "town").ToLowerInvariant() switch
        {
            "village" => rng.Next(60, 151),
            "city" => rng.Next(400, 1201),
            _ => rng.Next(150, 401),
        };
    }

    private ItemDef PickWeighted(List<ItemDef> pool, Random rng)
    {
        // Вес: расходники втрое чаще; дешёвые вещи встречаются чаще дорогих.
        var weights = pool.Select(d =>
        {
            var cat = Category(CategoryOf(d.Id))!;
            return (cat.Consumable ? 3.0 : 1.0) / Math.Sqrt(Math.Max(1, BasePrice(d.Id)));
        }).ToArray();

        var roll = rng.NextDouble() * weights.Sum();
        for (var i = 0; i < pool.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0)
            {
                return pool[i];
            }
        }

        return pool[^1];
    }

    private static string RollRarity(string tier, Random rng)
    {
        var r = rng.NextDouble();
        return tier switch
        {
            "village" => r < 0.01 ? "rare" : r < 0.09 ? "uncommon" : "common",
            "city" => r < 0.006 ? "epic" : r < 0.04 ? "rare" : r < 0.2 ? "uncommon" : "common",
            _ => r < 0.02 ? "rare" : r < 0.15 ? "uncommon" : "common",
        };
    }

    private static int StackSize(string id, string cat, Random rng)
    {
        if (Stacks.TryGetValue(id, out var byId) || Stacks.TryGetValue(cat, out byId))
        {
            return rng.Next(byId.Min, byId.Max + 1);
        }

        return 1;
    }

    public static string RarityNote(string? rarity) => Lang.IsEn ? (Genre.IsModern ? RarityKey(rarity) switch
    {
        "uncommon" => "Fitted and tuned by a craftsman.",
        "rare" => "Professional gear.",
        "epic" => "One-of-a-kind work, only a handful exist.",
        "legendary" => "A legendary item with its own history.",
        _ => "",
    } : Genre.IsCyber ? RarityKey(rarity) switch
    {
        "uncommon" => "Modded by street tinkerers.",
        "rare" => "Military spec.",
        "epic" => "Experimental corporate prototype.",
        "legendary" => "An iconic item with its own history.",
        _ => "",
    } : RarityKey(rarity) switch
    {
        "uncommon" => "Fine work.",
        "rare" => "Superb quality, a subtle enchantment.",
        "epic" => "Masterwork, a strong enchantment.",
        "legendary" => "A legendary item with its own history.",
        _ => "",
    }) : Genre.IsModern ? RarityKey(rarity) switch
    {
        "uncommon" => "Подогнана и доработана мастером.",
        "rare" => "Профессиональное снаряжение.",
        "epic" => "Штучная работа, таких единицы.",
        "legendary" => "Легендарная вещь с собственной историей.",
        _ => "",
    } : Genre.IsCyber ? RarityKey(rarity) switch
    {
        "uncommon" => "Доработана умельцами с улицы.",
        "rare" => "Армейская спецификация.",
        "epic" => "Экспериментальный корпоративный образец.",
        "legendary" => "Культовая вещь с собственной историей.",
        _ => "",
    } : RarityKey(rarity) switch
    {
        "uncommon" => "Добротная работа.",
        "rare" => "Превосходное качество, тонкое зачарование.",
        "epic" => "Мастерская работа, сильное зачарование.",
        "legendary" => "Легендарная вещь с собственной историей.",
        _ => "",
    };

    // ===== Сделки =====

    /// <summary>Купить qty штук товара лавки. Предмет кладётся в сумку (в клетку col,row, если она свободна).</summary>
    public bool Buy(RpgState state, MerchantShop shop, GridItem ware, int qty, int? col, int? row, out string message, out int paid)
    {
        paid = 0;
        qty = Math.Clamp(qty, 1, Math.Max(1, ware.Quantity));
        var unit = BuyPrice(shop, ware, state.Character);
        var total = unit * qty;
        if (state.Character.Gold < total)
        {
            message = Lang.T($"Не хватает золота: «{ware.Name}» x{qty} стоит {total}, у героя {state.Character.Gold}.", $"Not enough {Genre.Money}: \"{ware.Name}\" x{qty} costs {total}, the hero has {state.Character.Gold}.");
            return false;
        }

        var bought = CloneForTransfer(ware, qty);
        bought.Value = UnitValue(ware); // цена «запоминается» — при перепродаже считается от неё
        if (!Place(state.Grid, bought, col, row, RpgState.GridCols, RpgState.GridRows))
        {
            message = Lang.T($"В сумке нет места {ware.W}×{ware.H} для «{ware.Name}».", $"No {ware.W}×{ware.H} room in the bag for \"{ware.Name}\".");
            return false;
        }

        TakeFrom(shop.Items, ware, qty);
        state.Character.Gold -= total;
        shop.Gold += total;
        paid = total;
        message = Lang.T($"Куплено: «{ware.Name}»{(qty > 1 ? $" x{qty}" : "")} за {total} з.", $"Bought: \"{ware.Name}\"{(qty > 1 ? $" x{qty}" : "")} for {total} {Genre.Coin}.");
        return true;
    }

    /// <summary>Продать qty штук предмета из сумки героя в лавку.</summary>
    public bool Sell(RpgState state, MerchantShop shop, GridItem item, int qty, int? col, int? row, out string message, out int got)
    {
        got = 0;
        qty = Math.Clamp(qty, 1, Math.Max(1, item.Quantity));
        var unit = SellPrice(shop, item, state.Character);
        if (unit <= 0)
        {
            message = RefuseReason(shop, item);
            return false;
        }

        var total = unit * qty;
        if (shop.Gold < total)
        {
            message = Lang.T($"У торговца только {shop.Gold} з. — на «{item.Name}» x{qty} ({total} з.) не хватает.", $"The merchant has only {shop.Gold} {Genre.Coin} — not enough for \"{item.Name}\" x{qty} ({total} {Genre.Coin}).");
            return false;
        }

        var sold = CloneForTransfer(item, qty);
        sold.Value = UnitValue(item);
        if (!Place(shop.Items, sold, col, row, MerchantShop.ShopCols, MerchantShop.ShopRows))
        {
            message = Lang.T("В лавке нет места под этот предмет.", "The shop has no room for this item.");
            return false;
        }

        TakeFrom(state.Grid, item, qty);
        state.Character.Gold += total;
        shop.Gold -= total;
        got = total;
        message = Lang.T($"Продано: «{item.Name}»{(qty > 1 ? $" x{qty}" : "")} за {total} з.", $"Sold: \"{item.Name}\"{(qty > 1 ? $" x{qty}" : "")} for {total} {Genre.Coin}.");
        return true;
    }

    private static GridItem CloneForTransfer(GridItem src, int qty) => new()
    {
        Name = src.Name,
        Icon = src.Icon,
        W = src.W,
        H = src.H,
        Quantity = qty,
        Note = src.Note,
        Slot = src.Slot,
        TwoHanded = src.TwoHanded,
        Value = src.Value,
        Rarity = src.Rarity,
        Damage = src.Damage,
        Armor = src.Armor,
        Bonuses = new(src.Bonuses),
        Effects = new(src.Effects),
        Level = src.Level,
    };

    /// <summary>Уменьшает стопку или убирает предмет целиком.</summary>
    private static void TakeFrom(List<GridItem> items, GridItem item, int qty)
    {
        if (item.Quantity > qty)
        {
            item.Quantity -= qty;
        }
        else
        {
            items.Remove(item);
        }
    }

    /// <summary>Кладёт предмет на сетку: сначала докладывает в такую же стопку, иначе ищет место.</summary>
    private bool Place(List<GridItem> items, GridItem item, int? col, int? row, int cols, int rows)
    {
        var stackable = Category(CategoryOf(item.Icon))?.Consumable == true;
        if (stackable)
        {
            var same = items.FirstOrDefault(i => i.Icon == item.Icon && i.Name == item.Name && RarityKey(i.Rarity) == RarityKey(item.Rarity));
            if (same is not null)
            {
                same.Quantity += item.Quantity;
                return true;
            }
        }

        if (col is { } c && row is { } r)
        {
            c = Math.Clamp(c, 0, Math.Max(0, cols - item.W));
            r = Math.Clamp(r, 0, Math.Max(0, rows - item.H));
            col = c;
            row = r;
        }

        var spot = InventoryOps.FindFree(items, item.W, item.H, null, col, row, cols, rows);
        if (spot is null)
        {
            return false;
        }

        (item.Col, item.Row) = spot.Value;
        items.Add(item);
        return true;
    }

    /// <summary>Краткий справочник для промпта: типы торговцев и категории.</summary>
    public static string PromptReference() =>
        Lang.T("Типы торговцев (kind): ", "Merchant types (kind): ") + string.Join("; ", Merchants.Select(m => $"{m.Key} — {m.Title}")) + ". " +
        Lang.T("Категории товаров: ", "Goods categories: ") + string.Join(", ", Categories.Select(c => $"{c.Key} ({c.Title.ToLowerInvariant()})")) + ".";
}
