using System.Text;
using System.Text.Json;

namespace RPG_Harness.Services;

/// <summary>Запись каталога предметов: иконка + размер в клетках инвентаря-тетриса.</summary>
public sealed class ItemDef
{
    public string Id { get; set; } = "";

    /// <summary>Название из каталога (русское).</summary>
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string NameRu { get; set; } = "";

    /// <summary>Название на языке интерфейса: английское — из оверлея i18n/en.json, иначе русское.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Name
    {
        get => Lang.IsEn ? CatalogEn.Item(Id) ?? NameRu : NameRu;
        set => NameRu = value;
    }

    /// <summary>weapon | shield | armor | jewelry | potion | scroll | book | food | valuable | tool | misc.</summary>
    public string Cat { get; set; } = "misc";

    public int W { get; set; } = 1;
    public int H { get; set; } = 1;

    /// <summary>Слот экипировки (имя EquipSlot) или null.</summary>
    public string? Slot { get; set; }

    public bool TwoHanded { get; set; }

    /// <summary>Именной артефакт: в обычных лавках не продаётся, генератор добычи берёт его облик для эпиков и легендарок.</summary>
    public bool Art { get; set; }

    /// <summary>Спрайт нарисован по диагонали (оружие) — в высокой ячейке его поворачивают вертикально.</summary>
    public bool Diag { get; set; }

    /// <summary>Основы слов для угадывания иконки по названию предмета.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("kw")]
    public List<string> KwRu { get; set; } = new();

    /// <summary>Основы слов для подбора иконки: русские из каталога и, в английском интерфейсе, слова английского названия.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<string> Kw => Lang.IsEn ? KwRu.Concat(CatalogEn.Keywords(CatalogEn.Item(Id), Id)).ToList() : KwRu;

    public EquipSlot? EquipSlot => Enum.TryParse<EquipSlot>(Slot, out var s) ? s : null;

    /// <summary>Сеттинг вещи (Genre.*): пусто — фэнтези (основной каталог items.json).</summary>
    public string Genre { get; set; } = "";

    // Поля ниже есть только у каталогов, где механика записана в самом JSON (киберпанк):
    // фэнтезийные вещи берут урон, броню и цены из таблиц ItemStats и ItemEconomy.

    /// <summary>Базовая цена в деньгах сеттинга (0 — из таблицы ItemEconomy).</summary>
    public int Price { get; set; }

    /// <summary>Категория торговли (ключ ItemEconomy.Categories).</summary>
    public string? Trade { get; set; }

    /// <summary>Урон оружия («1d8 кинетический»).</summary>
    public string? Damage { get; set; }

    /// <summary>Броня к КБ.</summary>
    public int Armor { get; set; }

    /// <summary>Стрелковое оружие: атака и урон от ЛОВ.</summary>
    public bool Ranged { get; set; }

    /// <summary>Фехтовальное: лучшая из СИЛ и ЛОВ.</summary>
    public bool Finesse { get; set; }

    /// <summary>Вес доспеха: heavy — ЛОВ к КБ не идёт, medium — не больше +2.</summary>
    public string? Weight { get; set; }

    /// <summary>Только сюжетная вещь — не попадает в лавки и добычу.</summary>
    public bool StoryOnly { get; set; }
}

/// <summary>
/// Каталог пиксельных иконок (wwwroot/sprites/items.json, собирается tools/sprites/build_items.py).
/// Отдаёт размеры предметов по умолчанию и угадывает иконку по названию.
/// </summary>
public sealed class ItemCatalog
{
    private static readonly string[] CategoryOrder =
        { "weapon", "shield", "armor", "jewelry", "potion", "scroll", "book", "food", "valuable", "material", "tool", "misc" };

    private static Dictionary<string, string> CategoryTitles => Lang.IsEn ? CategoryTitlesEn : CategoryTitlesRu;

    private static readonly Dictionary<string, string> CategoryTitlesEn = new()
    {
        ["weapon"] = "weapons", ["shield"] = "shields", ["armor"] = "armor and clothing", ["jewelry"] = "jewelry",
        ["potion"] = "potions", ["scroll"] = "scrolls and maps", ["book"] = "books", ["food"] = "food",
        ["valuable"] = "valuables", ["material"] = "materials and trophies", ["tool"] = "tools", ["misc"] = "miscellaneous",
    };

    private static readonly Dictionary<string, string> CategoryTitlesRu = new()
    {
        ["weapon"] = "оружие", ["shield"] = "щиты", ["armor"] = "броня и одежда", ["jewelry"] = "украшения",
        ["potion"] = "зелья", ["scroll"] = "свитки и карты", ["book"] = "книги", ["food"] = "еда",
        ["valuable"] = "ценности", ["material"] = "материалы и трофеи", ["tool"] = "инструменты", ["misc"] = "прочее",
    };

    private readonly Dictionary<string, ItemDef> _byId;

    public ItemCatalog(IWebHostEnvironment env)
    {
        var items = new List<ItemDef>();
        // items.json — фэнтези (тайлы DCSS), cyber-items.json и modern-items.json — нарисованы кодом.
        foreach (var (file, genre) in new[] { ("items.json", Services.Genre.Fantasy), ("cyber-items.json", Services.Genre.Cyberpunk), ("modern-items.json", Services.Genre.Modern) })
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(env.WebRootPath, "sprites", file)));
                var part = doc.RootElement.GetProperty("items").Deserialize<List<ItemDef>>(
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new();
                foreach (var def in part)
                {
                    def.Genre = genre;
                }

                items.AddRange(part);
            }
            catch
            {
                // без каталога работаем с размерами 1x1 и иконкой-мешком
            }
        }

        _all = items.Where(i => i.Id != "avatar").ToList();
        _byGenre = _all.GroupBy(i => i.Genre).ToDictionary(g => g.Key, g => (IReadOnlyList<ItemDef>)g.ToList());
        _byId = items.GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        ItemStats.Register(_all);
    }

    private readonly List<ItemDef> _all;
    private readonly Dictionary<string, IReadOnlyList<ItemDef>> _byGenre;

    /// <summary>Вещи текущего сеттинга: их видит модель, их генерируют лавки и добыча.</summary>
    public IReadOnlyList<ItemDef> All => _byGenre.GetValueOrDefault(Services.Genre.Current) ?? Array.Empty<ItemDef>();

    /// <summary>Все вещи обоих сеттингов (для разбора сохранений и CSS).</summary>
    public IReadOnlyList<ItemDef> Everything => _all;

    public ItemDef? Get(string? id) =>
        id is not null && _byId.TryGetValue(id.Trim().Replace("-", "_"), out var d) ? d : null;

    public bool Exists(string? id) => Get(id) is not null;

    /// <summary>
    /// Подбирает иконку по названию. Очки предмета — суммарная длина совпавших основ:
    /// «Длинный лук» ближе к longbow (длинный + лук), чем к long_sword (только «длинный»).
    /// </summary>
    public ItemDef? Guess(string? name)
    {
        var n = (name ?? "").ToLowerInvariant().Replace('ё', 'е');
        if (n.Length == 0)
        {
            return null;
        }

        ItemDef? best = null;
        var bestScore = 0;
        foreach (var item in All)
        {
            var score = item.Kw.Select(k => k.Replace('ё', 'е')).Where(n.Contains).Sum(k => k.Length);
            if (score > bestScore)
            {
                best = item;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>Иконка по id или, если id неизвестен, по названию; иначе мешок.</summary>
    public ItemDef Resolve(string? icon, string? name) =>
        Get(icon) ?? Guess(name) ?? Get(Services.Genre.Pick("sack", "cy_bag", "md_bag")) ?? new ItemDef { Id = "sack", Name = Lang.T("Мешок", "Sack"), W = 2, H = 2 };

    /// <summary>Компактный список иконок с размерами для описания инструмента (id WxH).</summary>
    public string PromptList()
    {
        var sb = new StringBuilder();
        foreach (var cat in CategoryOrder)
        {
            var items = All.Where(i => i.Cat == cat).ToList();
            if (items.Count == 0)
            {
                continue;
            }

            sb.Append(CategoryTitles.GetValueOrDefault(cat, cat)).Append(": ");
            sb.Append(string.Join(" ", items.Select(i => $"{i.Id}{(i.Art ? "★" : "")}")));
            sb.Append(". ");
        }

        return sb.ToString().TrimEnd();
    }
}
