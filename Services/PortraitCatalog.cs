using System.Text;
using System.Text.Json;

namespace RPG_Harness.Services;

/// <summary>Лист портретов: файл в wwwroot/sprites и сетка клеток.</summary>
public sealed class PortraitSheet
{
    public string File { get; set; } = "";
    public int Cols { get; set; } = 4;
    public int Rows { get; set; } = 4;
}

/// <summary>Портрет: клетка листа + смысл (название, тип существа, основы слов для подбора по имени).</summary>
public sealed class PortraitDef
{
    public string Id { get; set; } = "";
    public string Sheet { get; set; } = "";
    public int Cell { get; set; }

    /// <summary>Название из каталога (русское).</summary>
    [System.Text.Json.Serialization.JsonPropertyName("title")]
    public string TitleRu { get; set; } = "";

    /// <summary>Название на языке интерфейса (английское — из оверлея i18n/en.json).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Title => Lang.IsEn ? CatalogEn.Portrait(Id) ?? TitleRu : TitleRu;

    /// <summary>Тип существа: humanoid, beast, undead, construct, dragon, demon, aberration, elemental, plant, ooze, giant, celestial, monstrosity.</summary>
    public string Kind { get; set; } = "humanoid";

    /// <summary>Подсказка облика: ordinary / elite / boss / legend.</summary>
    public string Tier { get; set; } = "ordinary";

    /// <summary>m / f / пусто.</summary>
    public string Gender { get; set; } = "";

    /// <summary>Лицо разумного персонажа — годится герою, спутнику и NPC.</summary>
    public bool Person { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("kw")]
    public List<string> KwRu { get; set; } = new();

    /// <summary>Основы слов для подбора по имени: русские из каталога и слова английского названия.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<string> Kw => Lang.IsEn ? KwRu.Concat(CatalogEn.Keywords(CatalogEn.Portrait(Id), Id)).ToList() : KwRu;

    /// <summary>Сеттинг облика (Genre.*): пусто — фэнтези.</summary>
    public string Genre { get; set; } = "";
}

/// <summary>
/// Каталог портретов (wwwroot/sprites/portraits.json, собирается tools/sprites/build_portraits.py).
/// Единое место, где решается, какой картинкой показан противник, спутник или герой.
/// </summary>
public static class PortraitCatalog
{
    private static readonly object Gate = new();
    private static Dictionary<string, PortraitSheet> _sheets = new();
    private static Dictionary<string, string> _kinds = new();
    private static List<PortraitDef> _all = new();
    private static Dictionary<string, PortraitDef> _byId = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    /// <summary>Листы, которые до каталога адресовались как «classic:N», «humanoid:N», «monster:N».</summary>
    private static readonly Dictionary<string, string> LegacySheets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["classic"] = "classic", ["humanoid"] = "humanoid", ["monster"] = "monster", ["companion"] = "companion",
    };

    public static void Load(string webRoot)
    {
        lock (Gate)
        {
            try
            {
                var path = Path.Combine(webRoot, "sprites", "portraits.json");
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
                _sheets = doc.RootElement.GetProperty("sheets").Deserialize<Dictionary<string, PortraitSheet>>(opts) ?? new();
                _kinds = doc.RootElement.GetProperty("kinds").Deserialize<Dictionary<string, string>>(opts) ?? new();
                _all = doc.RootElement.GetProperty("portraits").Deserialize<List<PortraitDef>>(opts) ?? new();
                _byId = _all.GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                // без каталога портреты подбираются старым способом (по хешу имени)
            }

            _loaded = true;
        }
    }

    private static void EnsureLoaded()
    {
        if (!_loaded)
        {
            Load(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
            if (_all.Count == 0)
            {
                Load(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));
            }
        }
    }

    /// <summary>Облики текущего сеттинга: из них модель выбирает и по ним подбирается портрет по имени.</summary>
    public static IReadOnlyList<PortraitDef> All
    {
        get
        {
            EnsureLoaded();
            var genre = RPG_Harness.Services.Genre.Current;
            return _all.Where(p => RPG_Harness.Services.Genre.Normalize(p.Genre) == genre).ToList();
        }
    }

    /// <summary>Лист лиц героя и спутников (их облики не предлагаем как врагов по умолчанию).</summary>
    private static bool PersonSheet(string sheet) => sheet is "companion" or "hero" or "cyber_persons" or "cyber_hero" or "modern_persons" or "modern_hero";

    public static IEnumerable<PortraitDef> People => All.Where(p => p.Person);

    public static string KindTitle(string? kind)
    {
        EnsureLoaded();
        return (Lang.IsEn ? CatalogEn.Kind(kind) : null) ?? _kinds.GetValueOrDefault(kind ?? "", kind ?? "");
    }

    /// <summary>Портрет по id; понимает и старые адреса «monster:3».</summary>
    public static PortraitDef? Get(string? id)
    {
        EnsureLoaded();
        var raw = (id ?? "").Trim();
        if (raw.Length == 0)
        {
            return null;
        }

        if (_byId.TryGetValue(raw, out var direct) || _byId.TryGetValue(raw.Replace(' ', '_'), out direct))
        {
            return direct;
        }

        var parts = raw.Split(':', 2);
        if (parts.Length == 2 && LegacySheets.TryGetValue(parts[0].Trim(), out var sheet) && int.TryParse(parts[1], out var cell))
        {
            return _all.FirstOrDefault(p => p.Sheet == sheet && p.Cell == Math.Clamp(cell, 0, 15));
        }

        return null;
    }

    private static string Norm(string? s) => (s ?? "").ToLowerInvariant().Replace('ё', 'е');

    /// <summary>Совпадение имени с основами слов портрета: сумма длин совпавших основ.</summary>
    public static int Score(PortraitDef p, string? name)
    {
        var n = Norm(name);
        return n.Length == 0 ? 0 : p.Kw.Select(Norm).Distinct().Where(k => k.Length > 1 && n.Contains(k)).Sum(k => k.Length);
    }

    /// <summary>Лучший портрет по имени («Снежный волк 2» → wolf_snow). Портреты героев/спутников — в последнюю очередь.</summary>
    public static (PortraitDef? Portrait, int Score) Guess(string? name, bool enemy = true)
    {
        EnsureLoaded();
        PortraitDef? best = null;
        var bestScore = 0;
        foreach (var p in All)
        {
            if (!enemy && !p.Person)
            {
                continue;
            }

            var score = Score(p, name) * 10 - (enemy && PersonSheet(p.Sheet) ? 5 : 0);
            if (score > bestScore)
            {
                best = p;
                bestScore = score;
            }
        }

        return (best, bestScore / 10);
    }

    /// <summary>
    /// Портрет противника: явный выбор модели, если он не противоречит имени; иначе — по имени;
    /// иначе — детерминированно из подходящих роли обликов. Note — пояснение для результата инструмента.
    /// </summary>
    public static (PortraitDef? Portrait, string Note) ResolveEnemy(string? requested, string? name, string? tier, string? kind = null)
    {
        EnsureLoaded();
        var asked = Get(requested);
        var (guess, score) = Guess(StripNumber(name));
        if (asked is not null)
        {
            if (guess is not null && guess.Id != asked.Id && score >= 4 && Score(asked, StripNumber(name)) == 0)
            {
                return (guess, Lang.T($"портрет «{asked.Id}» ({asked.Title}) не похож на «{name}» — подобран «{guess.Id}» ({guess.Title})", $"the portrait \"{asked.Id}\" ({asked.Title}) does not look like \"{name}\" — picked \"{guess.Id}\" ({guess.Title})"));
            }

            return (asked, "");
        }

        if (guess is not null && score >= 3)
        {
            return (guess, requested is { Length: > 0 }
                ? Lang.T($"портрета «{requested}» нет — подобран по имени «{guess.Id}» ({guess.Title})", $"there is no portrait \"{requested}\" — picked by name \"{guess.Id}\" ({guess.Title})")
                : Lang.T($"портрет подобран по имени: «{guess.Id}» ({guess.Title})", $"the portrait is picked by name: \"{guess.Id}\" ({guess.Title})"));
        }

        var want = tier switch
        {
            "arc_boss" => "legend",
            "dungeon_boss" or "quest_boss" => "boss",
            "elite" => "elite",
            _ => "ordinary",
        };
        var enemies = All.Where(p => !PersonSheet(p.Sheet)).ToList();
        var byKind = string.IsNullOrWhiteSpace(kind) ? enemies : enemies.Where(p => p.Kind.Equals(kind.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (byKind.Count == 0) byKind = enemies;
        var pool = byKind.Where(p => p.Tier == want).ToList();
        if (pool.Count == 0) pool = byKind;
        if (pool.Count == 0)
        {
            return (null, "");
        }

        var seed = Norm(StripNumber(name)).Aggregate(17, (h, ch) => unchecked(h * 31 + ch));
        var pick = pool[(int)((uint)seed % (uint)pool.Count)];
        return (pick, Lang.T($"по имени портрет не найден — взят облик «{pick.Id}» ({pick.Title}); укажи portrait, если он не подходит", $"no portrait found by name — took the look \"{pick.Id}\" ({pick.Title}); specify portrait if it does not fit"));
    }

    /// <summary>«Гоблин 2» → «Гоблин»: номер не должен влиять на подбор облика.</summary>
    private static string StripNumber(string? name) =>
        System.Text.RegularExpressions.Regex.Replace(name ?? "", @"\s*[#№]?\d+\s*$", "").Trim();

    /// <summary>Портрет спутника или NPC: явный id, иначе по расе/классу/полу.</summary>
    public static PortraitDef? ResolvePerson(string? requested, string? race, string? gender, string? charClass, string? seedName)
    {
        EnsureLoaded();
        if (Get(requested) is { Person: true } asked)
        {
            return asked;
        }

        // Пол известен — облик другого пола не подходит вовсе; раса весит больше класса.
        var g = string.IsNullOrWhiteSpace(gender) ? "" : HeroAvatars.RowFor(gender) == 1 ? "f" : "m";
        var ranked = People
            .Where(p => g.Length == 0 || p.Gender.Length == 0 || p.Gender == g)
            .Select(p => (p, s: Score(p, race) * 20 + Score(p, charClass) * 10 + (p.Sheet is "companion" or "cyber_persons" or "modern_persons" ? 1 : 0)))
            .OrderByDescending(x => x.s)
            .ToList();
        var top = ranked.FirstOrDefault();
        if (top.p is not null && top.s >= 30)
        {
            return top.p;
        }

        // Человек без особых примет — облик по полу, детерминированно по имени.
        var humans = People.Where(p => (g.Length == 0 || p.Gender == g) &&
                                       (p.Id.StartsWith("human") || p.Sheet is "hero" or "cyber_persons" or "cyber_hero" or "modern_persons" or "modern_hero")).ToList();
        if (humans.Count == 0)
        {
            return null;
        }

        var seed = Norm(seedName).Aggregate(7, (h, ch) => unchecked(h * 31 + ch));
        return humans[(int)((uint)seed % (uint)humans.Count)];
    }

    /// <summary>CSS-фон клетки портрета (background-size в процентах — масштабируется под любой размер).</summary>
    public static string Style(PortraitDef p)
    {
        EnsureLoaded();
        var sheet = _sheets.GetValueOrDefault(p.Sheet) ?? new PortraitSheet { File = "enemy-portraits.png" };
        var col = p.Cell % sheet.Cols;
        var row = p.Cell / sheet.Cols;
        var x = sheet.Cols == 1 ? 0 : col * 100.0 / (sheet.Cols - 1);
        var y = sheet.Rows == 1 ? 0 : row * 100.0 / (sheet.Rows - 1);
        return $"background-image:url('/sprites/{sheet.File}');background-size:{sheet.Cols * 100}% {sheet.Rows * 100}%;" +
               $"background-position:{x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}% " +
               $"{y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}%;background-repeat:no-repeat";
    }

    /// <summary>Стиль портрета противника (с подбором по имени для старых сохранений без id).</summary>
    public static string EnemyStyle(Adversary e)
    {
        var (p, _) = ResolveEnemy(e.Portrait, e.Name, e.ThreatTier, e.Kind);
        return p is null ? "" : Style(p);
    }

    /// <summary>Стиль портрета спутника.</summary>
    public static string MemberStyle(PartyMember m)
    {
        var p = ResolvePerson(m.Portrait, m.Race, m.Gender, m.CharClass, m.Name);
        return p is null ? "" : Style(p);
    }

    /// <summary>Справочник для описания update_adversary / plan_encounter: id — облик, сгруппировано по типу существа.</summary>
    public static string EnemyPromptList()
    {
        EnsureLoaded();
        var sb = new StringBuilder();
        foreach (var group in All.Where(p => !PersonSheet(p.Sheet)).GroupBy(p => p.Kind))
        {
            sb.Append(KindTitle(group.Key)).Append(": ");
            sb.Append(string.Join(", ", group.Select(p => $"{p.Id}={Short(p.Title)}{TierMark(p.Tier)}")));
            sb.Append(". ");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Справочник обликов для спутников и NPC.</summary>
    public static string PersonPromptList()
    {
        EnsureLoaded();
        return string.Join(", ", People.Select(p => $"{p.Id}={Short(p.Title)}{(p.Gender == "f" ? Lang.T("/ж", "/f") : p.Gender == "m" ? Lang.T("/м", "/m") : "")}"));
    }

    /// <summary>«Гоблин с кинжалом и щитом» → «гоблин»: суть облика без описания деталей.</summary>
    private static string Short(string title)
    {
        var t = title.ToLowerInvariant();
        foreach (var cut in new[] { " с ", " в ", " на ", " with ", " in ", " on " })
        {
            var i = t.IndexOf(cut, StringComparison.Ordinal);
            if (i > 0) t = t[..i];
        }

        return t.Trim();
    }

    private static string TierMark(string tier) => tier switch
    {
        "elite" => "*",
        "boss" => "**",
        "legend" => "***",
        _ => "",
    };
}
