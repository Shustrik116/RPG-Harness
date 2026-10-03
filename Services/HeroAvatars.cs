namespace RPG_Harness.Services;

/// <summary>
/// Портреты героя из листа <c>wwwroot/sprites/hero-avatars.png</c> (3 колонки × 2 строки):
/// колонки — воин, ловкач, маг; строки — мужской и женский.
/// Единственное место, где решается, какой портрет у героя: и книга героя, и окно боя берут его отсюда,
/// иначе они разъезжаются. Устаревший одиночный <c>avatar.png</c> больше не используется.
/// </summary>
public static class HeroAvatars
{
    /// <summary>Файл листа в wwwroot/sprites.</summary>
    public const string Sheet = "hero-avatars.png";

    /// <summary>Один портрет листа: id для сохранения в состоянии + позиция в сетке.</summary>
    public readonly record struct Option(string Id, string TitleRu, int Col, int Row, string TitleEn = "")
    {
        /// <summary>Подпись портрета на языке интерфейса.</summary>
        public string Title => Lang.IsEn && TitleEn.Length > 0 ? TitleEn : TitleRu;
    }

    /// <summary>Шесть портретов листа текущего сеттинга в порядке отображения в выборе.</summary>
    public static IReadOnlyList<Option> All => Genre.Pick(Fantasy, Cyber, Modern);

    /// <summary>Современность (modern-hero-avatars.png): боец, ловкач, специалист.</summary>
    private static readonly IReadOnlyList<Option> Modern =
    [
        new("md-hero-fighter-m", "Боец", 0, 0, "Fighter"),
        new("md-hero-rogue-m", "Ловкач", 1, 0, "Infiltrator"),
        new("md-hero-expert-m", "Специалист", 2, 0, "Specialist"),
        new("md-hero-fighter-f", "Боец", 0, 1, "Fighter"),
        new("md-hero-rogue-f", "Ловкачка", 1, 1, "Infiltrator"),
        new("md-hero-expert-f", "Специалистка", 2, 1, "Specialist"),
    ];

    /// <summary>Киберпанк (cyber-hero-avatars.png): соло, раннер, нетраннер.</summary>
    private static readonly IReadOnlyList<Option> Cyber =
    [
        new("cy-hero-solo-m", "Соло", 0, 0, "Solo"),
        new("cy-hero-runner-m", "Раннер", 1, 0, "Runner"),
        new("cy-hero-netrunner-m", "Нетраннер", 2, 0, "Netrunner"),
        new("cy-hero-solo-f", "Соло", 0, 1, "Solo"),
        new("cy-hero-runner-f", "Раннерша", 1, 1, "Runner"),
        new("cy-hero-netrunner-f", "Нетраннерша", 2, 1, "Netrunner"),
    ];

    private static readonly IReadOnlyList<Option> Fantasy =
    [
        new("warrior-male", "Воин", 0, 0, "Warrior"),
        new("rogue-male", "Ловкач", 1, 0, "Rogue"),
        new("mage-male", "Маг", 2, 0, "Mage"),
        new("warrior-female", "Воительница", 0, 1, "Warrior"),
        new("rogue-female", "Ловкачка", 1, 1, "Rogue"),
        new("mage-female", "Волшебница", 2, 1, "Mage"),
    ];

    /// <summary>Портрет по умолчанию — если состояние старое и портрет не выбран.</summary>
    public const string FallbackId = "warrior-male";

    private static readonly string[] MagicClasses =
        ["маг", "колдун", "чарод", "жрец", "друид", "некром", "алхим", "волшеб", "ведьм", "нетран", "хакер", "техник", "инженер", "медтех", "медик", "врач", "айтишн", "специалист",
         "mage", "wizard", "warlock", "sorcer", "cleric", "priest", "druid", "necroman", "alchem", "witch", "netrun", "hacker", "tech", "engineer", "medic", "doctor", "occult", "exorcist", "medium", "specialist"];

    private static readonly string[] AgileClasses =
        ["плут", "следоп", "рейндж", "вор", "убийц", "бард", "охотник", "детектив", "пилот", "раннер", "ниндзя", "кочевн", "снайпер", "разведч", "ловкач", "домушн", "киллер", "карманн",
         "rogue", "ranger", "thief", "assassin", "bard", "hunter", "detective", "pilot", "runner", "ninja", "nomad", "sniper", "scout", "burglar", "hitman", "pickpocket", "infiltrator"];

    /// <summary>Колонка по классу: 2 — магия, 1 — ловкач, 0 — воин.</summary>
    public static int ColumnFor(string? charClass)
    {
        var cls = (charClass ?? "").Trim().ToLowerInvariant();
        if (MagicClasses.Any(cls.Contains))
        {
            return 2;
        }

        return AgileClasses.Any(cls.Contains) ? 1 : 0;
    }

    /// <summary>Строка по полу: 1 — женский, 0 — мужской (и всё неизвестное).</summary>
    public static int RowFor(string? gender)
    {
        var g = (gender ?? "").Trim().ToLowerInvariant();
        return g.StartsWith("жен", StringComparison.Ordinal) ||
               g.StartsWith("female", StringComparison.Ordinal) || g.StartsWith("wom", StringComparison.Ordinal) ||
               g is "ж" or "f"
            ? 1
            : 0;
    }

    /// <summary>Подходящий портрет по классу и полу — им заполняется выбор при создании героя.</summary>
    public static string Suggest(string? charClass, string? gender) =>
        All[RowFor(gender) * 3 + ColumnFor(charClass)].Id;

    /// <summary>Человекочитаемое название портрета (для подписи в выборе).</summary>
    public static string TitleFor(string? id)
    {
        var option = All.FirstOrDefault(o => o.Id.Equals((id ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
        return option.Id is not null ? option.Title : PortraitCatalog.Get(id)?.Title ?? Lang.T("Герой", "Hero");
    }

    /// <summary>
    /// CSS-фон портрета. Неизвестный или пустой id (старые сохранения) заменяется подсказкой
    /// по классу и полу, поэтому портрет есть всегда.
    /// </summary>
    public static string Style(string? avatarId, string? charClass, string? gender)
    {
        var id = (avatarId ?? "").Trim();

        // Герою доступен любой облик персонажа из каталога (спутники, люди из листа врагов).
        if (PortraitCatalog.Get(id) is { Person: true } person)
        {
            return PortraitCatalog.Style(person);
        }

        var option = All.FirstOrDefault(o => o.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (option.Id is null)
        {
            option = All.First(o => o.Id == Suggest(charClass, gender));
        }

        // Аватары киберпанка и современности — клетки каталога портретов (своего листа у этого класса нет).
        if (PortraitCatalog.Get(option.Id) is { } cell && !Genre.IsFantasy)
        {
            return PortraitCatalog.Style(cell);
        }

        return $"background-image:url('/sprites/{Sheet}');background-size:300% 200%;" +
               $"background-position:{option.Col * 50}% {option.Row * 100}%;background-repeat:no-repeat";
    }
}
