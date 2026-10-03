using System.Text;
using System.Text.Json;

namespace RPG_Harness.Services;

/// <summary>
/// «Колесо дороги» — случайные события в пути. Бросок делает программа, а не модель:
/// d100 ≤ 60 — дорога прошла спокойно, иначе — событие. Значимость события зависит от дальности:
/// поход за город — мелочи и изредка что-то заметное, путь между городами и странами — шанс на крупное.
/// Масштаб таблиц — как в классических гекс-краулах: мелких находок много, логов и подземелий мало,
/// легендарные места — единицы на кампанию.
/// </summary>
public static partial class TravelEncounters
{
    /// <summary>Шанс, что в пути ничего не случится (на один бросок).</summary>
    public const int QuietChance = 60;

    /// <summary>Сколько последних событий не повторяется.</summary>
    public const int NoRepeatWindow = 12;

    public static readonly string[] Tiers = { "minor", "notable", "major", "legendary" };

    public static string TierTitle(string tier) => Lang.IsEn ? tier switch
    {
        "minor" => "minor",
        "notable" => "notable",
        "major" => "major",
        "legendary" => "legendary",
        _ => tier,
    } : tier switch
    {
        "minor" => "незначительное",
        "notable" => "заметное",
        "major" => "крупное",
        "legendary" => "легендарное",
        _ => tier,
    };

    public static string DistanceTitle(string distance) => Lang.IsEn ? (Genre.IsModern ? distance switch
    {
        "medium" => "across the city",
        "long" => "to another city",
        "epic" => "to another region or country",
        _ => "around the neighborhood",
    } : Genre.IsCyber ? distance switch
    {
        "medium" => "to a neighboring district",
        "long" => "across the whole megacity",
        "epic" => "to another city",
        _ => "within the block",
    } : distance switch
    {
        "medium" => "medium journey",
        "long" => "long road",
        "epic" => "journey between countries",
        _ => "short walk",
    }) : Genre.IsModern ? distance switch
    {
        "medium" => "через город",
        "long" => "в другой город",
        "epic" => "в другой регион или страну",
        _ => "по соседству",
    } : Genre.IsCyber ? distance switch
    {
        "medium" => "в соседний район",
        "long" => "через весь мегаполис",
        "epic" => "в другой город",
        _ => "в пределах квартала",
    } : distance switch
    {
        "medium" => "средний переход",
        "long" => "дальний путь",
        "epic" => "путь между странами",
        _ => "короткий переход",
    };

    /// <summary>Веса значимости событий по дальности: minor / notable / major / legendary.</summary>
    private static int[] TierWeights(string distance) => distance switch
    {
        "medium" => new[] { 80, 18, 2, 0 },
        "long" => new[] { 58, 30, 10, 2 },
        _ => new[] { 45, 33, 17, 5 }, // epic
    };

    /// <summary>Веса редкости добычи по значимости: нет / обычная / добротная / редкая / эпическая / легендарная.</summary>
    private static readonly string[] LootKeys = { "none", "common", "uncommon", "rare", "epic", "legendary" };

    private static int[] LootWeights(string tier) => tier switch
    {
        "minor" => new[] { 30, 48, 18, 4, 0, 0 },
        "notable" => new[] { 5, 22, 43, 25, 5, 0 },
        "major" => new[] { 0, 0, 22, 45, 29, 4 },
        _ => new[] { 0, 0, 0, 0, 40, 60 }, // legendary
    };

    public static string LootTitle(string key) => key == "none" ? Lang.T("без добычи", "no loot") : ItemEconomy.RarityTitle(key);

    /// <summary>Нормализует дальность: short / medium / long / epic.</summary>
    public static string NormalizeDistance(string? raw)
    {
        var d = (raw ?? "").Trim().ToLowerInvariant();
        return d switch
        {
            "short" or "близко" or "короткий" or "город" or "рядом" or "near" or "nearby" or "town" => "short",
            "medium" or "средний" or "средне" or "за город" or "окрестности" or "mid" => "medium",
            "long" or "дальний" or "далеко" or "между городами" or "far" => "long",
            "epic" or "very_long" or "очень далеко" or "между странами" or "экспедиция" or "expedition" or "very far" => "epic",
            _ => "",
        };
    }

    /// <summary>Нормализует местность к одному из кодов каталога (или пусто — любая).</summary>
    public static string NormalizeTerrain(string? raw)
    {
        var t = (raw ?? "").Trim().ToLowerInvariant();
        if (t.Length == 0) return "";
        if (Genre.IsCyber) return NormalizeCyberTerrain(t);
        if (Genre.IsModern) return NormalizeModernTerrain(t);
        if (t.Contains("лес") || t.Contains("forest") || t.Contains("чащ")) return "forest";
        if (t.Contains("гор") || t.Contains("mount") || t.Contains("перевал") || t.Contains("pass")) return "mountains";
        if (t.Contains("холм") || t.Contains("hill")) return "hills";
        if (t.Contains("болот") || t.Contains("топ") || t.Contains("swamp") || t.Contains("marsh") || t.Contains("bog")) return "swamp";
        if (t.Contains("пусты") || t.Contains("песк") || t.Contains("desert") || t.Contains("дюн") || t.Contains("dune") || t.Contains("sand")) return "desert";
        if (t.Contains("мор") || t.Contains("берег") || t.Contains("побереж") || t.Contains("coast") || t.Contains("sea") || t.Contains("река") || t.Contains("river")) return "coast";
        if (t.Contains("снег") || t.Contains("лед") || t.Contains("лёд") || t.Contains("тундр") || t.Contains("snow") || t.Contains("ice") || t.Contains("tundra") || t.Contains("glacier")) return "snow";
        if (t.Contains("степ") || t.Contains("равнин") || t.Contains("поле") || t.Contains("луг") || t.Contains("plain") || t.Contains("steppe") || t.Contains("field") || t.Contains("meadow")) return "plains";
        if (t.Contains("дорог") || t.Contains("тракт") || t.Contains("road") || t.Contains("highway") || t.Contains("track")) return "road";
        return "";
    }

    /// <summary>Результат одного броска колеса.</summary>
    public sealed record Spin(int D100, bool Event, string? Tier, Encounter? Encounter, string Loot, IReadOnlyList<string> Reel);

    /// <summary>
    /// Крутит колесо для перехода. short — без бросков; medium/long — один бросок; epic — два (путь в несколько этапов).
    /// recent — id недавних событий (не повторяются).
    /// </summary>
    public static List<Spin> Roll(string distance, string terrain, IReadOnlyCollection<string> recent, Random? rng = null)
    {
        rng ??= Random.Shared;
        var spins = new List<Spin>();
        var count = distance switch { "medium" or "long" => 1, "epic" => 2, _ => 0 };
        var used = new HashSet<string>(recent, StringComparer.Ordinal);

        for (var n = 0; n < count; n++)
        {
            var d100 = rng.Next(1, 101);
            var reel = BuildReel(distance, terrain, rng);
            if (d100 <= QuietChance)
            {
                spins.Add(new Spin(d100, false, null, null, "none", reel));
                continue;
            }

            var tier = Tiers[Weighted(TierWeights(distance), rng)];
            var pool = Catalog.Where(e => e.Tier == tier && !used.Contains(e.Id)).ToList();
            var local = pool.Where(e => e.Fits(terrain)).ToList();
            if (local.Count > 0)
            {
                pool = local;
            }

            if (pool.Count == 0)
            {
                pool = Catalog.Where(e => e.Tier == tier).ToList();
            }

            var enc = pool[rng.Next(pool.Count)];
            used.Add(enc.Id);
            var loot = enc.NoLoot ? "none" : LootKeys[Weighted(LootWeights(tier), rng)];
            reel[^1] = enc.Title;
            spins.Add(new Spin(d100, true, tier, enc, loot, reel));
        }

        return spins;
    }

    /// <summary>Лента «колеса» для анимации в чате: случайные названия, последнее — выпавшее.</summary>
    private static List<string> BuildReel(string distance, string terrain, Random rng)
    {
        var weights = TierWeights(distance);
        var reel = new List<string>();
        for (var i = 0; i < 9; i++)
        {
            if (rng.Next(100) < QuietChance)
            {
                reel.Add(QuietTitle);
                continue;
            }

            var tier = Tiers[Weighted(weights, rng)];
            var pool = Catalog.Where(e => e.Tier == tier && e.Fits(terrain)).ToList();
            if (pool.Count == 0) pool = Catalog.Where(e => e.Tier == tier).ToList();
            reel.Add(pool[rng.Next(pool.Count)].Title);
        }

        reel.Add(QuietTitle);
        return reel;
    }

    public static string QuietTitle => Genre.PickT("Спокойная дорога", "Спокойный путь", "Спокойная дорога", "Quiet road", "Quiet trip", "Quiet road");

    private static int Weighted(int[] weights, Random rng)
    {
        var total = weights.Sum();
        var roll = rng.Next(total);
        for (var i = 0; i < weights.Length; i++)
        {
            if (roll < weights[i]) return i;
            roll -= weights[i];
        }

        return weights.Length - 1;
    }

    /// <summary>Служебная строка для чата (анимация колеса) — первая строка результата инструмента.</summary>
    public const string MetaPrefix = "[[wheel ";

    public sealed record WheelMeta(string From, string To, string Distance, List<WheelSpin> Spins);

    public sealed record WheelSpin(int D100, bool Event, string? Tier, string? Title, string Loot, List<string> Reel);

    public static string BuildMeta(string from, string to, string distance, IEnumerable<Spin> spins) =>
        MetaPrefix + JsonSerializer.Serialize(new WheelMeta(from, to, distance,
            spins.Select(s => new WheelSpin(s.D100, s.Event, s.Tier, s.Encounter?.Title, s.Loot, s.Reel.ToList())).ToList())) + "]]";

    /// <summary>Результат без служебной строки анимации — то, что видит модель.</summary>
    public static string StripMeta(string result)
    {
        if (!result.StartsWith(MetaPrefix, StringComparison.Ordinal) && !result.StartsWith(RumorWheel.MetaPrefix, StringComparison.Ordinal))
        {
            return result;
        }

        var end = result.IndexOf("]]\n", StringComparison.Ordinal);
        return end < 0 ? result : result[(end + 3)..];
    }

    public static WheelMeta? ParseMeta(string? result)
    {
        if (string.IsNullOrEmpty(result) || !result.StartsWith(MetaPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var end = result.IndexOf("]]\n", StringComparison.Ordinal);
        if (end < 0 && result.EndsWith("]]", StringComparison.Ordinal)) end = result.Length - 2;
        if (end < 0) return null;

        try
        {
            return JsonSerializer.Deserialize<WheelMeta>(result[MetaPrefix.Length..end]);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Описание события для мастера: суть, как можно поступить, какую добычу выдать.</summary>
    public static string Describe(Spin spin, int index, int total)
    {
        var sb = new StringBuilder();
        var leg = total > 1 ? Lang.T($"Этап {index + 1} из {total}: ", $"Stage {index + 1} of {total}: ") : "";
        if (!spin.Event)
        {
            sb.Append(leg).Append($"d100 = {spin.D100} (≤ {QuietChance}) — {Genre.PickT("дорога прошла спокойно", "путь прошёл спокойно", "дорога прошла спокойно", "the road was quiet", "the trip was quiet", "the road was quiet")}. ")
              .Append(Genre.PickT("Опиши путь 1–3 фразами (пейзаж, погода, усталость, время в пути) без выдуманных происшествий.",
                  "Опиши путь 1–3 фразами (метро, такси-дрон, байк под дождём, неон, сколько заняло) без выдуманных происшествий.", "Опиши путь 1–3 фразами (машина, трасса, погода, музыка в салоне, сколько заняло) без выдуманных происшествий.",
                  "Describe the journey in 1–3 sentences (landscape, weather, fatigue, travel time) without invented incidents.",
                  "Describe the trip in 1–3 sentences (metro, drone taxi, a bike in the rain, neon, how long it took) without invented incidents.",
                  "Describe the trip in 1–3 sentences (the car, the highway, the weather, music in the car, how long it took) without invented incidents."));
            return sb.ToString();
        }

        var e = spin.Encounter!;
        if (Lang.IsEn)
        {
            sb.Append(leg).Append($"d100 = {spin.D100} (> {QuietChance}) — EVENT, significance: {TierTitle(spin.Tier!)}.\n")
              .Append($"\"{e.Title}\". {e.Situation}\n")
              .Append($"The hero's choice: {e.Choice}\n");
            if (spin.Loot == "none")
            {
                sb.Append(e.NoLoot ? "Loot: none intended." : "Loot: rolled \"no loot\" — at most worthless trifles or information.");
            }
            else
            {
                sb.Append($"Loot, if the hero sees it through: the best item is {LootTitle(spin.Loot).ToLowerInvariant()} ({spin.Loot}); {e.LootHint}. ")
                  .Append("The rest is trifles and coins according to the event's significance. Give out nothing more valuable than this rarity.");
            }

            return sb.ToString();
        }

        sb.Append(leg).Append($"d100 = {spin.D100} (> {QuietChance}) — СОБЫТИЕ, значимость: {TierTitle(spin.Tier!)}.\n")
          .Append($"«{e.Title}». {e.Situation}\n")
          .Append($"Выбор героя: {e.Choice}\n");
        if (spin.Loot == "none")
        {
            sb.Append(e.NoLoot ? "Добыча: не предусмотрена." : "Добыча: выпало «без добычи» — максимум мелочь без ценности или сведения.");
        }
        else
        {
            sb.Append($"Добыча, если герой доведёт дело до конца: лучшая вещь — {LootTitle(spin.Loot).ToLowerInvariant()} ({spin.Loot}); {e.LootHint}. ")
              .Append("Остальное — мелочь и монеты по значимости события. Ничего ценнее этой редкости не выдавай.");
        }

        return sb.ToString();
    }

    // ===================================================================
    //  Каталог событий. Terrains пуст — подходит любой местности.
    // ===================================================================

    public sealed record Encounter(string Id, string Tier, string Title, string Situation, string Choice, string LootHint,
        string[] Terrains, bool NoLoot = false)
    {
        public bool Fits(string terrain) =>
            Terrains.Length == 0 || terrain.Length == 0 || Terrains.Contains(terrain);
    }

    private static Encounter M(string id, string title, string situation, string choice, string loot, params string[] terrains) =>
        new(id, "minor", title, situation, choice, loot, terrains);

    private static Encounter N(string id, string title, string situation, string choice, string loot, params string[] terrains) =>
        new(id, "notable", title, situation, choice, loot, terrains);

    private static Encounter J(string id, string title, string situation, string choice, string loot, params string[] terrains) =>
        new(id, "major", title, situation, choice, loot, terrains);

    private static Encounter L(string id, string title, string situation, string choice, string loot, params string[] terrains) =>
        new(id, "legendary", title, situation, choice, loot, terrains);

    private static Encounter Quiet(string id, string tier, string title, string situation, string choice, params string[] terrains) =>
        new(id, tier, title, situation, choice, "", terrains, NoLoot: true);

    /// <summary>События текущего сеттинга.</summary>
    public static Encounter[] Catalog => Lang.IsEn
        ? Translated(Genre.Pick(FantasyCatalog, CyberCatalog, ModernCatalog), Genre.Pick(FantasyEn, CyberEn, ModernEn))
        : Genre.Pick(FantasyCatalog, CyberCatalog, ModernCatalog);

    private static readonly Encounter[] FantasyCatalog =
    {
        // ---------------- Незначительные: находки, мелкие встречи, приметы ----------------
        M("abandoned-camp", "Брошенный лагерь",
            "Остывшее кострище, рваный навес, опрокинутый котелок. Хозяева ушли в спешке — или их увели.",
            "Осмотреться (Внимательность/Выживание DC 10–12) или пройти мимо. Следы могут вывести к зацепке.",
            "забытая сумка: припасы, пара монет, личная вещь с именем"),
        M("lost-bundle", "Оброненный свёрток",
            "У обочины — узел, упавший с телеги: ткань перевязана бечёвкой, внутри что-то звякает.",
            "Забрать себе, оставить или попытаться найти владельца (он где-то впереди по дороге).",
            "товар мелкого торговца или чья-то посылка", "road", "plains"),
        M("wayside-shrine", "Придорожный алтарь",
            "Мшистый идол местного божка, у подножия — медяки, лента, засохший хлеб.",
            "Оставить подношение (удача: преимущество на одну проверку до заката) или забрать чужие дары (дурная примета, мастер вправе припомнить).",
            "горсть медяков и мелкий оберег"),
        M("ruined-watchtower", "Руины сторожевой башни",
            "Полуобвалившаяся башня у дороги. Лестница держится, наверху шуршат птицы.",
            "Подняться (Атлетика DC 10, при провале — ушиб 1d4) — оттуда видно окрестности и, возможно, старый тайник стражи. Можно пройти мимо.",
            "тайник дозорного: сигнальный рог, огниво, пара монет", "road", "hills", "plains"),
        M("dead-traveler", "Мёртвый путник",
            "В траве — давно погибший странник. Одежда истлела, в спине обломок стрелы. Рядом дорожный посох.",
            "Обыскать, похоронить (доброе дело, мелкая награда от судьбы) или уйти. Стрела — зацепка: чья?",
            "кошель, дорожные вещи, письмо или карта с пометкой"),
        M("rotten-chest", "Сундук под корнями",
            "Корни старого дерева обхватили окованный сундук — кто-то закопал его давно, дожди размыли землю.",
            "Вскрыть (Ловкость рук DC 12 или силой — Атлетика DC 13, шумно) или оставить.",
            "содержимое тайника: монеты, простое оружие или украшение", "forest", "hills", "plains"),
        M("trapped-chest", "Сундук с ловушкой",
            "Слишком удобно стоящий сундучок в расщелине. Замок блестит — новый, в отличие от самого сундука.",
            "Заметить ловушку (Внимательность DC 13), обезвредить (Ловкость рук DC 13) или рискнуть: игла с ядом 1d6 и отравление до отдыха. Можно не трогать.",
            "приманка охотника за сокровищами — монеты и одна приличная вещь"),
        M("broken-cart", "Сломанная телега",
            "Торговец сидит у телеги с треснувшей осью и ругается на лошадь. Спешит на ярмарку.",
            "Помочь (Атлетика или смекалка) — он отблагодарит; можно пройти мимо или поторговаться за помощь.",
            "скидка у этого торговца, пара монет или мелкий товар", "road"),
        M("lone-peddler", "Коробейник",
            "Одинокий разносчик с заплечным коробом: нитки, специи, амулеты «от всего», сплетни.",
            "Поторговать (можно создать лавку set_merchant kind=general tier=village), расспросить о дороге. Он может быть и шпионом.",
            "покупки по его ценам, слух о дороге", "road", "plains"),
        M("pilgrims", "Паломники",
            "Группа паломников бредёт к святыне, поют негромко. Предлагают разделить хлеб у костра.",
            "Присоединиться на привале (отдых, слухи, благословение) или вежливо отказаться.",
            "еда, слух, иногда святая вода или оберег", "road"),
        M("wounded-beast", "Раненый зверь",
            "Молодой олень (или пёс, или ястреб) бьётся в браконьерском силке.",
            "Освободить (Обращение с животными DC 11) — зверь может потом помочь; добить ради мяса и шкуры; уйти.",
            "мясо, шкура или благодарность природы", "forest", "plains", "hills"),
        M("herb-glade", "Поляна трав",
            "Солнечная поляна: зверобой, кровохлёбка, что-то редкое с серебристыми листьями.",
            "Собрать (Природа/Медицина DC 11; провал — перепутал с ядовитым двойником, отравление) или пройти.",
            "травы для зелий, изредка редкий алхимический ингредиент", "forest", "plains", "hills"),
        Quiet("clear-spring", "minor", "Чистый родник",
            "Ледяной родник под камнем, вода сладкая. Место явно почитают: вокруг цветные ленты.",
            "Отдохнуть и умыться — короткий отдых без помех; можно наполнить фляги.", "forest", "hills", "mountains"),
        M("old-well", "Заброшенный колодец",
            "У развалин хутора — колодец. Далеко внизу что-то блестит в воде.",
            "Спуститься по верёвке (Атлетика DC 12) или пройти. Внизу может оказаться не только блеск.",
            "утопленная монета-желание, перстень или ржавый ключ", "plains", "road"),
        Quiet("rotten-bridge", "minor", "Ветхий мост",
            "Подвесной мост через овраг: доски прогнили, канаты скрипят.",
            "Перейти осторожно (Акробатика DC 11, провал — падение 1d6 и потерянная вещь) или обойти, потеряв полдня.", "forest", "mountains", "hills"),
        Quiet("bad-weather", "minor", "Непогода",
            "Ливень, град или густой туман накрывают дорогу. Одежда промокла, видимость — десять шагов.",
            "Переждать в укрытии (потеря времени) или идти дальше (Выживание DC 12, иначе сбиться с пути и устать)."),
        M("big-tracks", "Следы крупного зверя",
            "Свежие следы, глубокие, как от бочки. Ободранная кора на высоте человеческого роста.",
            "Обойти стороной, затаиться или выследить (Выживание DC 13) — логово зверя рядом, это может стать заметным событием.",
            "шерсть/коготь для алхимика — если выследить и не нарваться", "forest", "hills", "mountains"),
        M("hungry-bandits", "Голодные разбойники",
            "Трое оборванцев с дубинами требуют «пошлину». Видно, что им страшнее, чем герою.",
            "Откупиться парой монет, запугать (Запугивание DC 10), разговорить или прогнать боем — они быстро сбегут.",
            "медяки, тупой нож, краденое", "road", "forest"),
        M("small-barrow", "Одинокий курган",
            "Невысокий курган с поваленным камнем. Кто-то уже копал, но бросил.",
            "Раскопать (шум, время; есть риск разбудить что-то или навлечь проклятие — Мудрость DC 12) или оставить мёртвых в покое.",
            "старая монета, бронзовая застёжка, иногда ржавый клинок", "plains", "hills"),
        M("stray-messenger", "Птица с письмом",
            "Почтовый голубь с перебитым крылом садится прямо к ногам. На лапке — запечатанный футляр.",
            "Прочитать (печать сломана — это заметят), доставить адресату за награду или выпустить.",
            "награда адресата или ценная сплетня"),
        M("smuggler-mark", "Знак контрабандистов",
            "На придорожном камне — свежевырезанный знак: круг и три черты. Такие метят тайники.",
            "Поискать тайник (Расследование DC 13) — хозяева могут вернуться; или запомнить знак.",
            "тайник: контрабанда (специи, табак, яды) и монеты", "road", "coast", "hills"),
        M("shepherd", "Пастух у костра",
            "Старый пастух греется у огня, собаки косятся на героя. Он знает все тропы и все легенды округи.",
            "Поговорить (слухи, короткая тропа, местная легенда о кладе) или пройти мимо.",
            "сведения, иногда кусок сыра и совет", "plains", "hills", "mountains"),
        M("bird-nest", "Гнездо редкой птицы",
            "На уступе — гнездо огнегрудки: алхимики платят за такие яйца серебром.",
            "Достать (Атлетика DC 13; родители атакуют — пара царапин) или оставить.",
            "яйца для алхимика", "forest", "mountains", "coast"),
        M("fox-thief", "Лис-воришка",
            "Ночью на привале рыжий лис утаскивает мешочек с едой — и что-то блестящее из сумки.",
            "Догнать (Скрытность/Выживание DC 12) — нора лиса полна чужих мелочей; или смириться с потерей.",
            "лисья нора: безделушки, бусины, чья-то пропавшая серёжка", "forest", "plains"),
        M("fallen-tree", "Завал на дороге",
            "Огромное дерево перегородило путь. Под ветвями виден раздавленный сундук повозки.",
            "Расчистить (Атлетика DC 14, долго) или обойти; под стволом — остатки чужого груза.",
            "уцелевший груз", "forest", "road"),
        M("lost-child", "Заблудившийся ребёнок",
            "Девочка лет семи плачет у дороги: пошла за козой и потерялась.",
            "Отвести домой (деревня в стороне от пути — крюк) — семья отблагодарит чем может; или показать дорогу и идти дальше.",
            "благодарность деревни: ночлег, еда, семейная безделица", "road", "plains", "forest"),
        M("fairy-ring", "Грибной круг",
            "Идеальный круг поганок, внутри трава выше и зеленее. Слышится тихий смех.",
            "Шагнуть внутрь (Мудрость DC 13: успех — странный сон и мелкий фейский дар; провал — потерян час и пропала одна вещь) или обойти.",
            "фейская безделица с причудой", "forest"),
        M("hero-statue", "Статуя забытого героя",
            "Заросшая плющом статуя воина. Надпись на постаменте полустёрта, но имя можно прочитать.",
            "Расчистить надпись (История DC 12) — сведения о прошлом мира, зацепка для арки. Можно пройти.",
            "сведения, иногда монета под постаментом", "road", "plains", "hills"),
        Quiet("crossroads", "minor", "Сбитый указатель",
            "Перекрёсток, указатель повален и повёрнут. Колеи расходятся в три стороны.",
            "Сориентироваться (Выживание DC 12; провал — день пути впустую) или спросить местных.", "road", "plains"),
        M("wandering-bard", "Бродячий бард",
            "Бард с потрёпанной лютней поёт на привале балладу — в ней подозрительно точно описано место клада.",
            "Послушать и расспросить (слух о сокровище — зацепка на будущее), угостить, прогнать.",
            "слух о тайнике; иногда он продаёт карту за монеты", "road"),
        M("hunter-cairn", "Охотничья пирамидка",
            "Каменная пирамидка на тропе: охотники оставляют там припасы для путников. По обычаю — взял, оставь своё.",
            "Взять и оставить что-то взамен (удача) или взять всё (местные охотники станут недружелюбны).",
            "стрелы, сушёное мясо, моток верёвки", "forest", "hills", "mountains", "snow"),
        M("dead-horse", "Павший конь",
            "Конь в богатой сбруе лежит у ручья. На седле — клеймо знатного дома, сумки сорваны.",
            "Осмотреть (следы: всадника увели), снять сбрую или сообщить дому (награда, но и вопросы).",
            "дорогое седло или сбруя, геральдическая пряжка", "road", "plains"),
        M("wild-bees", "Дикий улей",
            "Дупло гудит: дикие пчёлы, соты сочатся мёдом.",
            "Добыть мёд (Природа DC 12 с дымом; иначе укусы 1d4) или обойти.",
            "мёд и воск — ценятся у травниц", "forest", "plains"),
        M("poacher-trap", "Капкан браконьера",
            "Под листвой щёлкает железо. Капкан! (Внимательность DC 12, иначе урон 1d6 и хромота до отдыха.)",
            "Высвободиться, найти другие капканы и тайник браконьера или уйти.",
            "шкуры и снасти браконьера", "forest"),
        M("abandoned-hut", "Пустая хижина",
            "Хижина лесника: дверь не заперта, на столе миска с засохшей кашей, на полке дневник.",
            "Переночевать (отдых), прочитать дневник (последняя запись тревожная — зацепка) или уйти.",
            "дневник, инструменты, запас дров и еды", "forest", "hills", "snow"),
        Quiet("desert-mirage", "minor", "Мираж",
            "На горизонте — оазис с пальмами. Он не приближается уже час.",
            "Не поддаться (Мудрость DC 12; провал — крюк и лишняя жажда) или пойти к миражу — иногда под ним правда есть колодец.", "desert"),
        M("shipwreck-flotsam", "Выброшенный груз",
            "Волны выкинули на берег обломки и бочонок с печатью торгового дома.",
            "Вскрыть бочонок, поискать среди обломков, вернуть печать торговому дому за награду.",
            "вино, специи или ткань; изредка шкатулка", "coast"),
        M("ice-crevice", "Вмёрзшая находка",
            "В прозрачной стене ледника вмёрз человек в старинных доспехах, рука сжимает что-то.",
            "Вырубить (долго, шумно, Атлетика DC 13) или пройти. Лёд может хранить и не только мертвеца.",
            "старинный предмет в хорошей сохранности", "snow", "mountains"),
        M("quagmire", "Трясина",
            "Кочка под ногой уходит вниз. Трясина хватает за колени (Атлетика DC 12, провал — потерять вещь из сумки или уйти по пояс).",
            "Выбраться, спасти вещи; в трясине видно торчащий ремень чужой сумки.",
            "сумка сгинувшего путника", "swamp"),
        M("will-o-wisp", "Блуждающие огоньки",
            "В тумане пляшут синие огоньки и зовут голосами знакомых людей.",
            "Не идти (Мудрость DC 13) или пойти — огоньки ведут в топь, но иногда к кургану с кладом утопленников.",
            "клад утопленников, если выбраться", "swamp"),
        M("toll-patrol", "Дорожный патруль",
            "Разъезд стражи: проверка подорожной, пошлина, вопросы «кто, куда, зачем».",
            "Заплатить, договориться (Убеждение), обмануть (Обман DC 13) или объехать. Патруль делится новостями.",
            "новости и слухи; при конфликте — неприятности", "road"),
        M("wolf-for-farmers", "Волк у хутора",
            "Хуторяне просят прогнать волка, что режет овец. Предлагают что могут.",
            "Взяться (короткая охота, лёгкий бой с одним-двумя волками) или отказаться.",
            "плата хуторян, волчья шкура", "plains", "forest", "hills"),
        M("goblin-scavengers", "Гоблины-мусорщики",
            "Два-три гоблина роются в обломках повозки и спорят. Героя пока не заметили.",
            "Обойти, прогнать, подкрасться и подслушать (у них есть вести) или поторговаться — они продадут находки.",
            "их находки: ржавый кинжал, фляга, блестящие пуговицы", "forest", "hills", "road"),
        Quiet("mountain-goat", "minor", "Горный козёл",
            "Упрямый исполинский козёл перегородил узкую тропу над пропастью и не собирается уступать.",
            "Переждать, отвлечь едой, спугнуть (Запугивание/Обращение с животными DC 12) или лезть в обход (Атлетика DC 13).", "mountains", "hills"),
        M("roadside-graveyard", "Кладбище у дороги",
            "Сумерки, покосившиеся кресты, из-под одного доносится скрежет.",
            "Проверить (гуль-одиночка или просто падальщик-барсук), обойти или прочитать молитву.",
            "подношения на могилах (брать — дурно), вещи гуля", "road", "plains"),
        M("herbalist-hermit", "Травница в пути",
            "Сгорбленная травница собирает коренья и ворчит. Корзина полна.",
            "Помочь донести (благодарность — зелье), поторговать, расспросить о ядах и хворях края.",
            "простое зелье лечения или трава", "forest", "swamp", "plains"),
        M("lost-mule", "Отбившийся мул",
            "Навьюченный мул мирно жуёт траву у дороги. Хозяина нигде нет.",
            "Взять мула с грузом, поискать хозяина или отпустить. Хозяин может объявиться и обвинить в краже.",
            "поклажа: инструменты, ткань, припасы", "road", "hills", "mountains"),
        M("ancient-milestone", "Древний верстовой камень",
            "Камень с надписью на мёртвом языке. Под ним — выемка, прикрытая плитой.",
            "Прочитать (История/Магия DC 13), поднять плиту (Атлетика DC 12) или идти дальше.",
            "старинная монета или кусочек древней карты", "road", "plains", "desert"),
        Quiet("stargazing", "minor", "Звёздная ночь",
            "Ясная ночь, звезёдный дождь. Проводник (или сам герой) замечает странное созвездие.",
            "Вглядеться (Магия/Религия DC 12) — знамение, намёк на грядущее; или просто выспаться."),
        Quiet("fellow-traveler", "minor", "Попутчик",
            "Одинокий путник просит разрешения идти вместе: безопаснее вдвоём.",
            "Взять (он болтлив и знает слухи; возможно, у него свои тайны) или отказать.", "road"),

        // ---------------- Заметные: бои, логова, сделки, мелкие тайны ----------------
        N("bandit-ambush", "Засада разбойников",
            "На узком месте дорогу перегораживает бревно; из-за деревьев выходят 4–6 разбойников с главарём.",
            "Отдать часть добра, торговаться, блефовать, бежать (Атлетика/Акробатика DC 13) или драться. Главарь уважает силу.",
            "добыча с главаря: оружие получше, кошель, краденое с прошлых жертв", "road", "forest", "hills"),
        N("beast-den", "Логово зверя",
            "Пещера у ручья, вход усеян костями. Изнутри тянет мускусом — там медведь, волчица с выводком или что похуже.",
            "Обойти, выманить, подкрасться или сразиться. В глубине — вещи прежних жертв.",
            "вещи сгинувших путников, трофеи зверя", "forest", "hills", "mountains"),
        N("smuggler-cave", "Пещера контрабандистов",
            "Незаметный грот, свежие следы бочек. Внутри — склад и пара сторожей, а на входе растяжка с колокольчиком.",
            "Проскользнуть (Скрытность DC 13), договориться, сдать страже (награда) или обчистить.",
            "контрабанда, деньги, иногда запретный алхимический товар", "coast", "hills", "forest"),
        N("caravan-attack", "Караван в беде",
            "Впереди крики: на торговый караван напали (волки, разбойники или налётчики из враждебной фракции).",
            "Вмешаться (бой, спасение людей), помочь после боя или обойти стороной. Караванщик запомнит.",
            "награда караванщика, скидки в его торговом доме, связи", "road", "plains", "desert"),
        N("haunted-chapel", "Часовня с призраком",
            "Разрушенная часовня; по ночам в ней плачет призрак монахини, не отпетой по обряду.",
            "Узнать её историю и упокоить (Религия DC 13 + маленькое задание), изгнать силой или уйти до ночи.",
            "благословлённая вещь или реликвия часовни", "forest", "hills", "plains"),
        N("knight-tomb", "Гробница рыцаря",
            "Каменный склеп в холме: ступени вниз, пара ловушек и 1–3 беспокойных мертвеца.",
            "Спуститься и разграбить (или почтить), завалить вход, уйти. Мёртвые уважают тех, кто не грабит.",
            "меч или доспех павшего рыцаря", "hills", "plains", "forest"),
        N("witch-hut", "Избушка ведьмы",
            "Изба на подпорках, из трубы зелёный дым. Хозяйка знает, кто пришёл, ещё до стука.",
            "Сделка: услуга за зелье, проклятие снять/наложить, знание. Можно отказаться — она запомнит.",
            "зелье, оберег или тайное знание — по сделке", "forest", "swamp"),
        N("bounty-hunters", "Охотники за головами",
            "Трое наёмников с листовкой: разыскивают человека, очень похожего на героя.",
            "Доказать, что обознались, откупиться, сбежать, драться — или узнать, кто на самом деле объявил награду.",
            "снаряжение наёмников, листовка-зацепка", "road"),
        N("bridge-troll", "Тролль под мостом",
            "Каменный мост; под ним живёт тролль, требует плату: золото, загадку или что-то вкусное.",
            "Заплатить, загадать загадку (Интеллект DC 13), обмануть, найти брод (время) или драться (опасно).",
            "накопленная троллем «плата» прежних путников", "road", "forest", "hills"),
        N("hermit-mage", "Маг-отшельник",
            "Одинокая башенка в глуши, хозяин — старый маг, одичавший и подозрительный.",
            "Пройти его испытание (загадка, услуга), выторговать знание или уйти. Может научить заклинанию.",
            "свиток, магическая безделушка или обучение", "forest", "mountains", "hills"),
        N("old-battlefield", "Старое поле битвы",
            "Поле ржавого железа, курганы павших. Ночью над ним тлеет холодный свет.",
            "Поискать уцелевшее оружие днём (Внимательность DC 13), избегать ночёвки — или остаться и встретить призраков.",
            "уцелевшее оружие или знамя с историей", "plains", "hills"),
        N("nomad-camp", "Стойбище кочевников",
            "Шатры кочевого племени (орки, степняки, кентавры). Воины уже заметили героя.",
            "Прийти с миром (Убеждение, дары), торговать, обойти стороной. Оскорбить — значит нажить врагов.",
            "торговля, проводник, возможно — союзник", "plains", "desert", "hills"),
        N("star-stone", "Упавшая звезда",
            "Ночью в холмах вспыхнуло — утром там дымящийся кратер со светящимся камнем. К нему уже идут и другие.",
            "Добраться первым (гонка), договориться с конкурентами или отступить.",
            "звёздный металл — ценен кузнецам и магам", "plains", "hills", "desert", "snow"),
        N("old-mine", "Заброшенная шахта",
            "Штольня с гнилыми креплениями, в глубине — жила и чьё-то логово (кобольды, пауки).",
            "Исследовать (обвалы, ловушки, обитатели), взять руду у входа или уйти.",
            "руда, самоцветы, инструменты старателей", "mountains", "hills"),
        N("caged-prisoner", "Пленник в клетке",
            "На поляне — клетка, внутри избитый пленник, умоляет о помощи. Похоже на приманку.",
            "Освободить (замок DC 13; засада рядом?), расспросить с расстояния или уйти. Пленник может стать союзником — или предать.",
            "благодарность пленника: сведения, связи, иногда вещь из тайника", "forest", "road"),
        N("honor-duel", "Рыцарь у моста",
            "Странствующий рыцарь поклялся не пропускать никого, кто не скрестит с ним меч.",
            "Принять поединок (до первой крови), уговорить (Убеждение DC 14), найти другой путь.",
            "уважение рыцаря и его дар, либо его конь/щит при победе", "road", "plains"),
        N("wishing-well", "Колодец желаний",
            "Каменный колодец шепчет: «Брось монету — и проси». Вокруг кости тех, кто просил слишком много.",
            "Бросить монету (Мудрость DC 14: малая удача или подвох), уйти, попытаться разгадать проклятие.",
            "малое благословение или проклятие — по броску", "forest", "plains"),
        N("harpy-nest", "Гнездо гарпий",
            "Над перевалом кружат гарпии, их песня тянет к обрыву (Мудрость DC 13).",
            "Проскочить (Скрытность/скорость), драться, затыкать уши воском. В гнезде — вещи сорвавшихся путников.",
            "вещи павших путников", "mountains", "coast"),
        N("shallow-wreck", "Корабль на мели",
            "Разбитый корабль лежит на отмели. В трюме вода и крабы с человека размером.",
            "Обыскать (опасно: приливы, твари), продать место находки, уйти.",
            "судовая касса, навигационный прибор, груз", "coast"),
        N("oasis-djinn", "Оазис с хитрым духом",
            "Оазис, в пальмах живёт мелкий дух огня, обожает сделки с подвохом.",
            "Сыграть с ним (загадки, спор), попросить воды, прогнать (опасно).",
            "дар духа: огненный амулет или запас воды", "desert"),
        N("bog-hag", "Болотная карга",
            "Старуха зовёт к себе на гать: «Помоги бабушке, милок». Туман сгущается.",
            "Раскусить (Проницательность DC 14), сторговаться, бежать или драться.",
            "склянки карги, заколдованная кукла", "swamp"),
        N("fog-village", "Деревня в тумане",
            "Из тумана выступает деревня, которой нет на картах. Жители приветливы, но время здесь течёт иначе.",
            "Погостить (сутки превращаются в неделю или наоборот), разгадать тайну, уйти до заката.",
            "дар жителей — если разгадать тайну", "forest", "plains", "swamp"),
        N("deserters", "Дезертиры",
            "Отряд дезертиров с награбленным. Устали, злы, боятся погони.",
            "Договориться (они щедро заплатят за молчание), сдать властям, драться.",
            "награбленное: оружие, монеты, чужие ценности", "road", "forest"),
        N("cult-ritual", "Ритуал культистов",
            "Ночью в роще — факелы, пение, связанный человек на камне.",
            "Сорвать ритуал (бой или хитрость), проследить за культом (зацепка), уйти.",
            "культовые реликвии, записи с именами", "forest", "hills"),
        N("strange-circus", "Бродячий цирк",
            "Пёстрые фургоны, клоуны с неподвижными улыбками. Приглашают на представление.",
            "Посмотреть (азартные игры, призы), разнюхать (в клетке не зверь), уйти.",
            "выигрыш, диковинка, спасённое существо", "road"),
        N("werewolf-moon", "Полнолуние",
            "Вой со всех сторон. По следам — волки размером с телёнка, и одни следы вдруг становятся человеческими.",
            "Укрыться до утра, выследить оборотня (зацепка), драться (риск заразиться ликантропией).",
            "серебряный кинжал охотника, трофей", "forest", "hills"),
        N("myconid-grotto", "Грот грибного народа",
            "Пещера мягко светится, микониды общаются спорами. Мирные, если их не топтать.",
            "Торговать спорами и знаниями, пройти насквозь (короткий путь), напугать.",
            "редкие грибы и споры для алхимии", "forest", "mountains", "swamp"),
        N("gate-golem", "Страж древних ворот",
            "Каменный голем у заросших ворот; по надписи — пропускает тех, кто назовёт слово.",
            "Найти слово (История/Магия DC 14), обойти, сразиться (он силён). За воротами — малые руины.",
            "находки малых руин", "hills", "mountains", "desert"),
        N("crossroads-fair", "Ярмарка на перекрёстке",
            "На перекрёстке стихийная ярмарка: торговцы, кулачные бои, стрельба на приз.",
            "Торговать (можно создать 1–2 лавки), участвовать в состязании (Атлетика/стрельба) — приз хороший.",
            "приз состязания, редкие товары у торговцев", "road", "plains"),
        N("dying-courier", "Раненый гонец",
            "Всадник падает с коня у ног героя: стрелы в спине, в руке — депеша для важной персоны.",
            "Доставить (награда и сюжетный поворот), прочитать, спрятать. За гонцом идут убийцы.",
            "награда адресата, покровительство", "road", "plains"),
        N("sand-worm-tracks", "Песчаная буря",
            "Буря закрывает небо; в песке под ногами проходит что-то большое.",
            "Укрыться (Выживание DC 14), бежать к скалам; после бури открываются занесённые руины.",
            "находки из раскрытых бурей руин", "desert"),
        N("ice-wolves", "Стая ледяных волков",
            "Белые волки идут следом третий день, выжидая, когда путник ослабнет.",
            "Отпугнуть огнём, дать бой вожаку, запутать след (Выживание DC 14).",
            "шкура вожака — ценится", "snow", "mountains"),

        // ---------------- Крупные: подземелья, логова чудовищ, крепости ----------------
        J("ancestral-crypt", "Склеп древнего рода",
            "Фамильный склеп угасшего рода: 3–4 зала, ловушки с механизмами, нежить и страж-рыцарь в последнем зале.",
            "Можно разведать и отступить в любой момент, кроме последнего зала (дверь запирается). Можно не входить вовсе.",
            "фамильная реликвия рода и казна склепа", "hills", "forest", "plains"),
        J("young-dragon", "Логово молодого дракона",
            "Обугленные деревья, выжженная земля, запах серы. Молодой дракон спит на куче монет.",
            "Прокрасться и унести что-то одно (Скрытность DC 16), договориться (драконы тщеславны), уйти — или сразиться (очень опасно).",
            "драконья сокровищница", "mountains", "hills", "swamp", "desert"),
        J("wizard-tower-ruins", "Руины башни мага",
            "Полуразрушенная башня, в которой ещё работают охранные чары: иллюзии, големы, запертые двери со словами-ключами.",
            "Исследовать по этажам (можно отступать между этажами), уйти. Разгадка лучше грубой силы.",
            "свитки, посох или жезл, магическая книга", "forest", "hills", "mountains"),
        J("bandit-fortress", "Крепость разбойничьего барона",
            "Бывший форт на скале, 15–20 бандитов, барон-самозванец и пленники в подвале.",
            "Штурм (почти самоубийство в одиночку), проникновение, переговоры, наём союзников в ближайшем городе. Можно обойти.",
            "казна барона, освобождённые пленники (связи), трофейное оружие", "hills", "forest", "road"),
        J("forgotten-temple", "Храм забытого бога",
            "Храм, поглощённый джунглями или песком. Испытания веры, жрецы-мумии, алтарь, который ещё отвечает.",
            "Пройти испытания (разум, дух, сила), осквернить (гнев бога) или уйти.",
            "священный артефакт или дар бога", "forest", "desert", "swamp"),
        J("giant-lair", "Логово великана",
            "Холм оказывается хижиной великана. Внутри — пленные крестьяне, откормленные к празднику.",
            "Спасти пленных хитростью, договориться (великаны любят спор и игры), драться (очень опасно), уйти.",
            "мешок великана: странные находки и сокровища", "hills", "mountains"),
        J("fey-gate", "Врата в страну фей",
            "Каменная арка в круге берёз светится на рассвете. За ней — двор фейской королевы.",
            "Войти (торг, опасные сделки, время течёт иначе), отказаться — арка закроется навсегда.",
            "фейский дар с условием", "forest"),
        J("warlord-tomb", "Гробница полководца",
            "Курган великого полководца, стражи-призраки его дружины испытывают достоинство вошедшего.",
            "Пройти испытание чести (без грабежа), сразиться с дружиной, уйти.",
            "оружие или доспех полководца", "plains", "hills"),
        J("hunting-manticore", "Мантикора на охоте",
            "Мантикора выбрала героя добычей и преследует уже второй день, отстреливаясь шипами.",
            "Найти логово и ударить первым, устроить засаду, уйти в город, договориться (она разумна и тщеславна).",
            "содержимое логова, яд шипов", "mountains", "desert", "hills"),
        J("pirate-cove", "Пиратская бухта",
            "Бухта с кораблём под чёрным флагом, в пещерах — склад добычи и таверна для своих.",
            "Внедриться, торговать с пиратами, сдать флоту, ограбить склад, пройти мимо.",
            "пиратская добыча, карта сокровищ", "coast"),
        J("dwarven-forge", "Кузня древних дварфов",
            "Заброшенный подгорный цех: механизмы ещё работают, автоматоны-стражи ходят по кругу.",
            "Разобраться в механизмах (Интеллект/инструменты), красться, драться, уйти.",
            "дварфийская броня или оружие, чертежи", "mountains", "hills"),
        J("sand-necropolis", "Песчаный некрополь",
            "Буря открыла город мёртвых: ступенчатые гробницы, стражи-скорпионы, проклятие на сокровищах.",
            "Исследовать до следующей бури (таймер!), взять малое и уйти, не входить.",
            "золото фараонов, проклятый амулет", "desert"),
        J("drowned-city", "Затопленный город",
            "Под водой озера — шпили старого города. Засуха обнажила купол храма.",
            "Нырять (дыхание, водные твари), найти воздушный карман, уйти.",
            "сокровища затопленного храма", "coast", "swamp", "plains"),
        J("ice-citadel", "Ледяная цитадель",
            "Во льдах — цитадель зимней ведьмы, её слуги из снега и замёрзшие пленники.",
            "Проникнуть, выторговать пропуск, растопить (огонь — ключ), уйти.",
            "зимний артефакт, освобождённые пленники", "snow", "mountains"),
        J("salamander-forge", "Вулканическая кузня",
            "Кузня саламандр в жерле вулкана: они куют для того, кто принесёт редкий металл.",
            "Сделка (заказ оружия), кража, бой в жару (усталость), уйти.",
            "огненное оружие по заказу", "mountains", "desert"),
        J("maze-forest", "Лес-лабиринт",
            "Тропы сплетаются в кольцо; хозяин леса, древний дух, играет с путником.",
            "Разгадать правила игры (Мудрость/Выживание DC 15), выторговать выход, сжечь (гнев леса).",
            "дар хозяина леса", "forest"),

        // ---------------- Легендарные: единицы на кампанию ----------------
        L("fallen-empire-vault", "Сокровищница павшей империи",
            "Многоярусное хранилище исчезнувшей империи: элитные стражи-конструкты, смертельные ловушки, печати. На нижнем ярусе — реликвия императоров.",
            "Спуститься ярус за ярусом (между ярусами можно уйти и вернуться с союзниками), отступить, продать сведения о входе.",
            "легендарная реликвия империи и казна яруса"),
        L("ancient-dragon", "Спящий древний дракон",
            "Гора — это логово древнего дракона. Он спит веками; сокровищница размером с собор.",
            "Украсть одну вещь (Скрытность DC 18), разбудить и говорить (смертельно опасно, но драконы ценят учтивость), уйти.",
            "одна легендарная вещь из сокровищницы", "mountains", "snow", "desert"),
        L("sword-in-stone", "Клинок в камне",
            "На острове посреди озера — камень с клинком. Страж-призрак спрашивает: «Зачем тебе меч?»",
            "Пройти испытание достоинства (ответ + деяние), сразиться со стражем, уйти.",
            "легендарный клинок (только достойному)"),
        L("sky-ruin", "Упавший небесный остров",
            "Небесный остров рухнул в долину: руины летающего города, чары ещё действуют, гравитация шалит.",
            "Исследовать (опасно, соперники-мародёры уже в пути), взять малое, уйти.",
            "артефакт небесного народа"),
        L("lich-tomb", "Гробница короля-лича",
            "Склеп короля, ставшего личом. Его филактерия и посох лежат в зале, который сам лич охраняет.",
            "Разведать и уйти (сведения ценны), подготовиться (союзники, освящённое оружие), штурмовать. Без плана — смерть.",
            "легендарный посох лича, уничтоженная филактерия — слава"),
        L("god-forge", "Кузня бога",
            "Остывшая кузня божества-кузнеца: молот ещё тёплый. Огонь можно разжечь, если принести жертву.",
            "Разжечь (цена!), выковать одну вещь, уйти. Бог может заметить.",
            "легендарная вещь, выкованная героем"),
        L("precursor-ark", "Ковчег предтеч",
            "Под землёй — ковчег древней расы: механизмы, стражи, знания, способные изменить мир.",
            "Проникнуть (загадки, стражи), взять знание или вещь, запечатать, уйти.",
            "легендарный артефакт предтеч", "mountains", "desert", "snow"),
        L("world-tree-heart", "Сердце древнего леса",
            "Древо, старше королевств. Хранитель-энт спрашивает, зачем пришёл путник.",
            "Помочь лесу (задание), попросить дар, попытаться взять силой (гнев всего леса).",
            "легендарный дар леса: лук, посох или семя", "forest"),
        L("ghost-ship", "Корабль-призрак",
            "В тумане — корабль-призрак легендарного капитана. Команда приглашает подняться на борт.",
            "Сыграть с капитаном (кости, загадки), освободить экипаж от проклятия, бежать.",
            "легендарный клинок или компас капитана", "coast"),
        L("demon-gate", "Врата преисподней",
            "Разлом, из которого тянет серой; у врат лежит оружие героя древности, павшего, запечатывая их.",
            "Забрать оружие (стражи-демоны, печать ослабнет), укрепить печать, уйти.",
            "легендарное оружие павшего героя"),
    };

    /// <summary>Краткая справка для описания инструмента.</summary>
    public static string PromptReference() =>
        Lang.T($"В каталоге {Catalog.Length} событий: незначительные ({Catalog.Count(e => e.Tier == "minor")}), заметные ({Catalog.Count(e => e.Tier == "notable")}), " +
        $"крупные ({Catalog.Count(e => e.Tier == "major")}), легендарные ({Catalog.Count(e => e.Tier == "legendary")}).",
        $"The catalog has {Catalog.Length} events: minor ({Catalog.Count(e => e.Tier == "minor")}), notable ({Catalog.Count(e => e.Tier == "notable")}), " +
        $"major ({Catalog.Count(e => e.Tier == "major")}), legendary ({Catalog.Count(e => e.Tier == "legendary")}).");
}
