namespace RPG_Harness.Services;

/// <summary>
/// Генератор добычи: что падает с противников или лежит в тайнике — по уровню, роли и типу существа.
/// Звери дают трофеи (клыки, шкуры, мясо), а не мечи; гуманоиды — мелочь из карманов, трофейное оружие,
/// зелья и золото; драконы — чешую и клад. Магические вещи (добротные и выше) идут через копилку
/// RpgState.LootMeter: их частота честна в среднем, а больше половины подбирается под слоты группы.
/// Бонусы вещи растут с её уровнем (ItemStats.Budget), поэтому добыча эндгейма остаётся желанной.
/// </summary>
public static partial class LootTables
{
    public sealed record LootResult(List<GridItem> Items, int Gold, List<string> Notes);

    /// <summary>Трофей: иконка, шаблон названия ({0} — существо), шанс, количество.</summary>
    private sealed record Trophy(string Icon, string Name, double Chance, int Min, int Max, string Note = "");

    private static Dictionary<string, Trophy[]> Trophies => Genre.Pick(FantasyTrophies, CyberTrophies, ModernTrophies);

    /// <summary>Трофеи современности: со зверей — шкуры и мясо, с машин — запчасти и топливо, с дронов — электроника.</summary>
    private static readonly Dictionary<string, Trophy[]> ModernTrophies = new()
    {
        ["beast"] = new Trophy[]
        {
            new("md_hide", "Шкура ({0})", 0.5, 1, 1, "Скупщики и охотники берут."),
            new("md_meat", "Мясо ({0})", 0.4, 1, 2, "Съедобно, если приготовить."),
            new("md_fang", "Клыки ({0})", 0.3, 1, 2, "Трофей на память или на продажу."),
        },
        ["vehicle"] = new Trophy[]
        {
            new("md_parts", "Запчасти ({0})", 0.8, 1, 2, "В автосервисе возьмут."),
            new("md_battery", "Аккумулятор ({0})", 0.4, 1, 1, ""),
            new("md_canister", "Канистра бензина", 0.4, 1, 1, "Полная, если повезло."),
        },
        ["drone"] = new Trophy[]
        {
            new("md_electronics", "Электроника ({0})", 0.8, 1, 1, "Платы и камера — в магазин электроники."),
            new("md_battery", "Аккумулятор ({0})", 0.5, 1, 1, ""),
        },
    };

    /// <summary>Трофеи киберпанка: с машин и киборгов снимают детали, с программ — код, с мутантов — образцы.</summary>
    private static readonly Dictionary<string, Trophy[]> CyberTrophies = new()
    {
        ["cyborg"] = new Trophy[]
        {
            new("cy_circuit", "Нейрочип ({0})", 0.5, 1, 1, "Снят с киборга; риппердоки берут охотно."),
            new("cy_servo", "Сервоприводы ({0})", 0.5, 1, 2, "Детали протезов."),
            new("cy_scrap", "Хромированные детали", 0.6, 1, 2, ""),
        },
        ["android"] = new Trophy[]
        {
            new("cy_circuit", "Процессорный модуль ({0})", 0.7, 1, 1, "Мозги андроида."),
            new("cy_polymer", "Синтекожа", 0.4, 1, 1, ""),
            new("cy_battery", "Силовой элемент ({0})", 0.4, 1, 1, ""),
        },
        ["drone"] = new Trophy[]
        {
            new("cy_battery", "Аккумулятор ({0})", 0.7, 1, 1, "Ещё держит заряд."),
            new("cy_lens", "Оптика ({0})", 0.4, 1, 1, ""),
            new("cy_wires", "Проводка ({0})", 0.5, 1, 2, ""),
        },
        ["mech"] = new Trophy[]
        {
            new("cy_scrap", "Бронепластины ({0})", 0.8, 1, 3, "Тяжёлая броня, лом на продажу."),
            new("cy_servo", "Сервопривод ({0})", 0.5, 1, 2, ""),
            new("cy_core", "Силовое ядро ({0})", 0.15, 1, 1, "Ценная деталь — техники отдадут хорошие эдди."),
        },
        ["program"] = new Trophy[]
        {
            new("cy_code", "Фрагмент кода ({0})", 0.9, 1, 2, "Нетраннеры платят за такое."),
            new("cy_shard", "Ключ шифрования", 0.3, 1, 1, "Может открыть чужой сервер."),
        },
        ["mutant"] = new Trophy[]
        {
            new("cy_biosample", "Образец тканей ({0})", 0.7, 1, 1, "Биолаборатории покупают."),
            new("cy_tissue", "Мутировавшая ткань", 0.5, 1, 2, ""),
        },
        ["beast"] = new Trophy[]
        {
            new("cy_servo", "Кибер-имплант ({0})", 0.4, 1, 1, "Вживлён в зверя."),
            new("cy_biosample", "Образец ({0})", 0.4, 1, 1, ""),
        },
    };

    private static readonly Dictionary<string, Trophy[]> FantasyTrophies = new()
    {
        ["beast"] = new Trophy[]
        {
            new("fang", "Клыки ({0})", 0.7, 1, 2, "Трофей охотника; алхимики и кожевники берут охотно."),
            new("fur", "Шкура ({0})", 0.6, 1, 1, "Выделывается в кожу или мех."),
            new("raw_meat", "Мясо ({0})", 0.4, 1, 2, "Съедобно, если приготовить."),
        },
        ["monstrosity"] = new Trophy[]
        {
            new("fang", "Когти ({0})", 0.6, 1, 2, "Трофей и алхимический реагент."),
            new("leather", "Шкура ({0})", 0.4, 1, 1, "Прочная шкура чудовища."),
            new("horned_skull", "Череп ({0})", 0.2, 1, 1, "Внушительный трофей."),
        },
        ["dragon"] = new Trophy[]
        {
            new("scales", "Чешуя ({0})", 1.0, 2, 5, "Материал для драконьего доспеха."),
            new("fang", "Драконий клык ({0})", 0.8, 1, 2, "Ценнейший трофей."),
            new("horned_skull", "Рога ({0})", 0.4, 1, 1, "Трофей, достойный легенды."),
        },
        ["undead"] = new Trophy[]
        {
            new("bones", "Кости ({0})", 0.6, 1, 1, "Некроманты платят за такие."),
            new("phial", "Могильная пыль", 0.3, 1, 2, "Реагент для тёмных зелий."),
            new("skull", "Череп ({0})", 0.2, 1, 1, ""),
        },
        ["construct"] = new Trophy[]
        {
            new("ingot", "Слиток ({0})", 0.8, 1, 2, "Металл конструкта."),
            new("gizmo", "Шестерни и пружины", 0.5, 1, 3, "Для изобретателей."),
            new("crystals", "Кристалл-ядро ({0})", 0.3, 1, 1, "Источник силы конструкта."),
        },
        ["demon"] = new Trophy[]
        {
            new("horned_skull", "Рога ({0})", 0.6, 1, 1, "Пахнут серой."),
            new("crystals", "Осколок адского камня", 0.3, 1, 2, "Тёплый на ощупь."),
            new("rune_abyss", "Руна бездны", 0.08, 1, 1, "Опасный артефакт иного мира."),
        },
        ["elemental"] = new Trophy[]
        {
            new("crystals", "Эссенция ({0})", 0.9, 1, 3, "Сгусток стихии: реагент для зачарований."),
            new("gem", "Самоцвет стихии", 0.3, 1, 1, ""),
        },
        ["ooze"] = new Trophy[]
        {
            new("phial", "Эссенция ({0})", 0.7, 1, 2, "Едкая субстанция для алхимии."),
        },
        ["plant"] = new Trophy[]
        {
            new("herbs", "Травы ({0})", 0.7, 1, 3, "Целебное сырьё."),
            new("mushroom", "Споры ({0})", 0.6, 1, 3, "Алхимический реагент."),
        },
        ["aberration"] = new Trophy[]
        {
            new("crystals", "Осколок бездны", 0.4, 1, 2, "Шепчет, если прислушаться."),
            new("orb", "Глаз ({0})", 0.3, 1, 1, "Мутная сфера, всё ещё следит."),
            new("void_sigil", "Печать бездны", 0.08, 1, 1, "Знак нечеловеческого разума."),
        },
        ["giant"] = new Trophy[]
        {
            new("fur", "Меха ({0})", 0.5, 1, 1, ""),
            new("horned_skull", "Трофейный череп", 0.2, 1, 1, ""),
        },
        ["celestial"] = new Trophy[]
        {
            new("fur", "Перья ({0})", 0.7, 1, 3, "Светятся в темноте."),
            new("crystals", "Осколок небесного света", 0.4, 1, 1, ""),
        },
    };

    /// <summary>Шанс, что у существа есть снаряжение (гуманоиды носят вещи, звери — нет).</summary>
    private static double GearChance(string kind) => Genre.IsModern ? kind switch
    {
        "humanoid" => 1.0,
        "vehicle" => 0.4,
        "drone" => 0.1,
        _ => 0.03,
    } : Genre.IsCyber ? kind switch
    {
        "humanoid" => 1.0,
        "cyborg" => 0.8,
        "android" => 0.5,
        "mutant" => 0.2,
        "mech" => 0.15,
        "drone" => 0.1,
        "program" => 0.3,
        _ => 0.05,
    } : kind switch
    {
        "humanoid" => 1.0,
        "dragon" => 0.9,
        "undead" or "giant" or "celestial" => 0.5,
        "demon" => 0.35,
        "construct" or "aberration" => 0.15,
        _ => 0.05,
    };

    /// <summary>Шанс золота и множитель клада.</summary>
    private static (double Chance, double Mult) GoldOf(string kind) => Genre.IsModern ? kind switch
    {
        "humanoid" => (0.8, 1),
        "vehicle" => (0.4, 1.2),
        "drone" => (0.05, 0.2),
        _ => (0.02, 0.1),
    } : Genre.IsCyber ? kind switch
    {
        "humanoid" => (0.8, 1),
        "cyborg" => (0.6, 1),
        "program" => (0.5, 1),
        "android" => (0.3, 0.6),
        "mutant" => (0.2, 0.4),
        "mech" or "drone" => (0.1, 0.4),
        _ => (0.05, 0.2),
    } : kind switch
    {
        "humanoid" => (0.8, 1),
        "dragon" => (1.0, 4),
        "undead" or "giant" or "demon" => (0.5, 0.8),
        "ooze" => (0.3, 0.5),
        _ => (0.1, 0.4),
    };

    /// <summary>
    /// Вклад врага в копилку магической добычи (RpgState.LootMeter). Обычный бой «три рядовых» даёт ~0,45 —
    /// находка примерно раз в 2–3 боя, около трёх на уровень (600 опыта ≈ 7–8 боёв).
    /// </summary>
    private static double MeterGain(string role) => role switch
    {
        "minion" => 0.05,
        "elite" => 0.4,
        "quest_boss" => 1.0,
        "dungeon_boss" => 1.5,
        "arc_boss" => 2.0,
        "chest" => 0.6,
        "hoard" => 2.0,
        _ => 0.15,
    };

    /// <summary>Сколько магических вещей гарантирует источник, даже если копилку недавно опустошили.</summary>
    private static int MagicGuarantee(string role) => role switch
    {
        "quest_boss" => 1,
        "dungeon_boss" or "hoard" => 2,
        "arc_boss" => 3,
        _ => 0,
    };

    /// <summary>Шанс трофейного (обычного, без бонусов) снаряжения — то, чем враг дрался.</summary>
    private static double WornChance(string role) => role switch
    {
        "minion" => 0.05,
        "elite" => 0.4,
        "quest_boss" or "dungeon_boss" or "arc_boss" => 0.6,
        "chest" => 0.5,
        "hoard" => 0.8,
        _ => 0.15,
    };

    /// <summary>Шанс найти в карманах мелочь на продажу и сколько штук.</summary>
    private static (double Chance, int Min, int Max) PocketRolls(string role) => role switch
    {
        "minion" => (0.15, 1, 1),
        "elite" => (0.7, 1, 1),
        "quest_boss" or "dungeon_boss" or "arc_boss" => (1.0, 1, 2),
        "chest" => (0.8, 1, 2),
        "hoard" => (1.0, 2, 2),
        _ => (0.3, 1, 1),
    };

    /// <summary>Таблица редкости магической находки по роли: uncommon, rare, epic, legendary (в процентах).</summary>
    private static double[] MagicTable(string role) => role switch
    {
        "elite" => new double[] { 65, 30, 5, 0 },
        "quest_boss" or "chest" => new double[] { 30, 55, 15, 0 },
        "dungeon_boss" or "hoard" => new double[] { 0, 55, 40, 5 },
        "arc_boss" => new double[] { 0, 25, 60, 15 },
        _ => new double[] { 85, 14, 1, 0 },
    };

    private static readonly string[] MagicKeys = { "uncommon", "rare", "epic", "legendary" };

    /// <summary>Редкость магической находки. Удача и уровень чуть сдвигают вероятность вверх: +1% за 5 уровней и +3% за единицу удачи.</summary>
    public static string RollRarity(string role, int level, double luck, Random rng)
    {
        var t = MagicTable(role).ToArray();
        var shift = level / 5.0 + luck * 3;
        for (var i = 0; i < t.Length - 1 && shift > 0; i++)
        {
            var move = Math.Min(t[i], shift);
            t[i] -= move;
            t[i + 1] += move;
            shift -= move * 0.5;
        }

        var roll = rng.NextDouble() * t.Sum();
        for (var i = 0; i < t.Length; i++)
        {
            roll -= t[i];
            if (roll <= 0) return MagicKeys[i];
        }

        return MagicKeys[^1];
    }

    /// <summary>Мелочь из карманов: иконка, название, базовая цена. Дешёвое попадается чаще дорогого.</summary>
    private sealed record Pocket(string Icon, string Name, int Price, string Note = "");

    private static Dictionary<string, Pocket[]> Pockets => Genre.Pick(FantasyPockets, CyberPockets, ModernPockets);

    private static readonly Dictionary<string, Pocket[]> ModernPockets = new()
    {
        ["humanoid"] = new Pocket[]
        {
            new("md_cigs", "Пачка сигарет", 2),
            new("md_lighter", "Зажигалка", 1),
            new("md_burner", "Одноразовый телефон", 5, "Пара номеров в контактах — может пригодиться."),
            new("md_phone", "Чужой смартфон", 12, "Заблокирован. Если взломать — переписка."),
            new("md_car_keys", "Ключи от машины", 2, "Где-то стоит машина, которая к ним подходит."),
            new("md_keychain", "Связка ключей", 1),
            new("md_photo", "Фотография", 1),
            new("md_lottery", "Лотерейный билет", 1, "Стереть защитный слой?"),
            new("md_flash_drive", "Флешка", 3, "Мало ли что на ней."),
            new("md_jewelry", "Золотая цепочка", 25, "Сдать в ломбард."),
        },
        ["chest"] = new Pocket[]
        {
            new("md_flashlight", "Фонарик", 3),
            new("md_rope", "Моток верёвки", 1),
            new("md_multitool", "Мультитул", 6),
            new("md_radio", "Рация", 20),
            new("md_binoculars", "Бинокль", 15),
            new("md_jewelry", "Ювелирные украшения", 40),
            new("md_cigar_box", "Коробка сигар", 30),
            new("md_docs", "Папка с документами", 10, "Чьи-то договоры и подписи."),
        },
    };

    private static readonly Dictionary<string, Pocket[]> CyberPockets = new()
    {
        ["humanoid"] = new Pocket[]
        {
            new("cy_cigs", "Мятая пачка сигарет", 2),
            new("cy_dice", "Игральные кости", 2, "Чья-то удача, теперь твоя."),
            new("cy_soda", "Банка газировки", 1),
            new("cy_energy", "Энергетик", 2),
            new("cy_lighter", "Зажигалка", 1),
            new("cy_keycard", "Ключ-карта", 2, "Неизвестно, от какой двери."),
            new("cy_phone", "Разбитый холофон", 6, "Можно сдать на запчасти или покопаться в памяти."),
            new("cy_photo", "Старое фото", 1),
            new("cy_shard", "Шард с записями", 3, "Личные заметки, контакты, может — компромат."),
            new("cy_holofig", "Голофигурка", 12, "Коллекционная безделушка."),
            new("cy_watch", "Золотые часы", 25),
        },
        ["chest"] = new Pocket[]
        {
            new("cy_cable", "Кабель", 1),
            new("cy_flashlight", "Фонарь", 3),
            new("cy_multitool", "Мультитул", 6),
            new("cy_scanner", "Сканер", 20),
            new("cy_binoculars", "Цифровой бинокль", 15),
            new("cy_holofig", "Голофигурка", 20),
            new("cy_crystal", "Кристалл памяти", 25),
            new("cy_vinyl", "Виниловая пластинка", 20, "Настоящий винил — редкость."),
        },
    };

    private static readonly Dictionary<string, Pocket[]> FantasyPockets = new()
    {
        ["humanoid"] = new Pocket[]
        {
            new("bone", "Костяные игральные кости", 2, "Чья-то удача, теперь твоя."),
            new("bottle", "Фляга дешёвого эля", 2),
            new("jerky", "Вяленое мясо", 1),
            new("bread", "Чёрствый хлеб", 1),
            new("torch", "Факел", 1),
            new("rope", "Моток верёвки", 1),
            new("key", "Ржавый ключ", 1, "Неизвестно, от чего."),
            new("mirror", "Медное зеркальце", 6),
            new("box", "Табакерка", 8),
            new("idol", "Резной оберег", 10, "Деревенский талисман на удачу."),
            new("ornate_flask", "Помятая серебряная фляга", 12),
            new("gem_amber", "Кусочек янтаря", 20),
        },
        ["undead"] = new Pocket[]
        {
            new("box", "Истлевшая шкатулка", 8),
            new("mirror", "Потускневшее зеркальце", 6),
            new("idol", "Погребальная статуэтка", 15, "Старинная работа; коллекционеры ценят."),
            new("gem_pale", "Бледный камень из глазницы", 30),
        },
        ["chest"] = new Pocket[]
        {
            new("rope", "Моток верёвки", 1),
            new("torch", "Факел", 1),
            new("lantern", "Фонарь", 8),
            new("mirror", "Серебряное зеркальце", 10),
            new("box", "Резная шкатулка", 12),
            new("idol", "Статуэтка", 20),
            new("compass", "Компас", 20),
            new("gem_amber", "Кусочек янтаря", 20),
        },
    };

    // ===== Свойства предметов по редкости =====

    private static readonly string[] MinorEffects =
    {
        "не ржавеет и не тупится", "светится в темноте", "тёплый: холод не страшен", "лёгкий: не мешает скрытности",
        "преимущество на Выживание в дикой местности", "не промокает и не горит",
    };

    private static readonly string[] WeaponEffects =
    {
        "удар поджигает: +{d} огнём", "удар леденит: +{d} холодом", "удар разрядом: +{d} электричеством", "яд на клинке: +{d} ядом",
        "против нежити: +{d} светом", "критическое попадание на 19–20", "вампиризм: лечит 1d6 при попадании",
        "раз в бой — оглушающий удар (спасбросок ТЕЛ)",
    };

    private static readonly string[] ArmorEffects =
    {
        "сопротивление огню", "сопротивление холоду", "сопротивление яду", "сопротивление некротике",
        "преимущество на спасброски от страха", "раз в бой — щит на 10% макс. ХП", "шаги бесшумны", "иммунитет к оглушению",
    };

    private static readonly string[] JewelryEffects =
    {
        "видит в темноте на 20 шагов", "раз в день — заклинание Щит (+5 КБ до конца раунда)", "+1 ко всем спасброскам",
        "раз в бой — восстановить 1К маны", "сопротивление психическому урону", "раз в день — благословение на группу",
    };

    private static readonly string[] UniqueEffects =
    {
        "невидимость ночью, пока не атакуешь", "раз в день — вернуться из смерти с 1 ХП", "полёт на минуту раз в день",
        "призыв духа-волка раз в бой", "понимает любой язык", "раз в день — остановить время на 1 раунд",
    };

    /// <summary>Сила «+Nd6» свойств растёт с уровнем предмета.</summary>
    private static string EffectDice(int level) => level < 10 ? "1d6" : level < 20 ? "2d6" : "3d6";

    private static void AddEffects(GridItem item, Random rng)
    {
        var rarity = ItemEconomy.RarityKey(item.Rarity);
        var limit = ItemStats.EffectLimit(rarity);
        if (limit == 0) return;

        var pool = item.Damage.Length > 0 ? WeaponEffects
            : item.Slot is EquipSlot.Amulet or EquipSlot.Ring1 or EquipSlot.Ring2 ? JewelryEffects
            : ArmorEffects;
        var picks = new List<string>();
        if (rarity == "uncommon")
        {
            picks.Add(MinorEffects[rng.Next(MinorEffects.Length)]);
        }
        else
        {
            var count = rarity == "rare" ? 1 : rarity == "epic" ? 2 : 2;
            foreach (var e in pool.OrderBy(_ => rng.Next()).Take(count)) picks.Add(e);
            if (rarity == "legendary") picks.Add(UniqueEffects[rng.Next(UniqueEffects.Length)]);
        }

        item.Effects = picks.Take(limit).Select(e => Tr(e).Replace("{d}", EffectDice(item.Level))).ToList();
    }

    // ===== Генерация =====

    /// <summary>Какие категории снаряжения носит существо.</summary>
    private static string[] GearCats(string kind) => Genre.IsModern ? kind switch
    {
        "vehicle" => new[] { "weapon", "armor", "valuable" },
        "drone" => new[] { "valuable" },
        "beast" => new[] { "valuable" },
        _ => new[] { "weapon", "weapon", "armor", "armor", "jewelry" },
    } : Genre.IsCyber ? kind switch
    {
        "cyborg" => new[] { "jewelry", "jewelry", "weapon", "armor" },
        "android" => new[] { "weapon", "armor", "jewelry" },
        "drone" => new[] { "weapon", "valuable" },
        "mech" => new[] { "weapon", "armor", "valuable" },
        "program" => new[] { "valuable" },
        "mutant" => new[] { "valuable", "jewelry" },
        "beast" => new[] { "jewelry" },
        _ => new[] { "weapon", "weapon", "armor", "armor", "jewelry", "shield" },
    } : kind switch
    {
        "dragon" => new[] { "weapon", "armor", "jewelry", "valuable", "valuable" },
        "undead" => new[] { "weapon", "armor", "jewelry" },
        "demon" or "aberration" => new[] { "jewelry", "valuable", "weapon" },
        "giant" => new[] { "weapon", "armor", "valuable" },
        "construct" => new[] { "valuable" },
        _ => new[] { "weapon", "weapon", "armor", "armor", "shield", "jewelry" },
    };

    private static readonly string[] SlotCats = { "weapon", "armor", "shield", "jewelry" };

    /// <summary>Цены растут на 10% за уровень вещи, как и её бюджет бонусов.</summary>
    private static double PriceMult(int level) => 1 + (Math.Max(1, level) - 1) / 10.0;

    /// <summary>
    /// Потолок базовой цены обычной (не именной) вещи в добыче: на 1 ур. — 75 з, на 10 — 300.
    /// Иначе добротный золотой амулет с гоблина стоит как десять боёв золота.
    /// </summary>
    private static int PriceCap(int level) => 50 + 25 * Math.Max(1, level);

    /// <summary>Случайная запись каталога: дешёвые основы встречаются чаще дорогих.</summary>
    private static ItemDef PickDef(List<ItemDef> pool, ItemEconomy economy, Random rng)
    {
        var weights = pool.Select(d => 1 / Math.Sqrt(Math.Max(1, economy.BasePrice(d.Id)))).ToArray();
        var roll = rng.NextDouble() * weights.Sum();
        for (var i = 0; i < pool.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0) return pool[i];
        }

        return pool[^1];
    }

    /// <summary>Отбор основ для добычи: без сюжетных вещей, по потолку цены; эпик и легендарка в 70% — именной облик.</summary>
    private static List<ItemDef> GearPool(ItemCatalog catalog, ItemEconomy economy, Func<ItemDef, bool> fits, string rarity, int level, Random rng)
    {
        var wantArt = rarity is "epic" or "legendary" && rng.NextDouble() < 0.7;
        var cap = PriceCap(level);
        var all = catalog.All.Where(d => d.Slot is not null && fits(d)).ToList();
        var art = all.Where(d => d.Art).ToList();
        var plain = all.Where(d => !d.Art && !ItemEconomy.StoryOnly.Contains(d.Id) && economy.BasePrice(d.Id) <= cap).ToList();
        if (plain.Count == 0) plain = all.Where(d => !d.Art && !ItemEconomy.StoryOnly.Contains(d.Id)).ToList();
        return wantArt && art.Count > 0 ? art : plain;
    }

    /// <summary>Собирает вещь из записи каталога: база, бонусы и свойства по редкости, цена по уровню.</summary>
    private static GridItem BuildGear(ItemDef def, ItemEconomy economy, string rarity, int level, Random rng)
    {
        var item = new GridItem
        {
            Name = def.Name, Icon = def.Id, W = def.W, H = def.H, Slot = def.EquipSlot, TwoHanded = def.TwoHanded,
            Rarity = def.Slot is null ? "common" : rarity, Level = Math.Max(1, level), Quantity = 1,
        };
        ItemStats.ApplyBase(item);
        if (item.Slot is not null)
        {
            ItemStats.RollBonuses(item, rng);
            AddEffects(item, rng);
            ItemStats.Balance(item);
            if (rarity != "common" && !def.Art) item.Name = $"{def.Name} ({ItemEconomy.RarityTitle(rarity)})";
            item.Note = ItemEconomy.RarityNote(rarity);
        }

        item.Value = Math.Max(1, (int)Math.Round(economy.UnitValue(item) * PriceMult(item.Level)));
        return item;
    }

    /// <summary>Случайная вещь из того, что носит существо (category — weapon/armor/shield/jewelry/valuable).</summary>
    private static GridItem MakeGear(ItemCatalog catalog, ItemEconomy economy, string cat, string rarity, int level, Random rng)
    {
        var pool = cat == "valuable"
            ? catalog.All.Where(d => d.Cat == "valuable" && !ItemEconomy.StoryOnly.Contains(d.Id) && economy.BasePrice(d.Id) <= PriceCap(level)).ToList()
            : GearPool(catalog, economy, d => d.Cat == cat, rarity, level, rng);
        if (pool.Count == 0) pool = catalog.All.Where(d => d.Cat == cat).ToList();
        if (pool.Count == 0) pool = catalog.All.Where(d => d.Cat == "valuable").ToList();
        return BuildGear(PickDef(pool, economy, rng), economy, rarity, level, rng);
    }

    /// <summary>
    /// Находка «под группу»: вещь для слота, который реально носит герой или спутник, того же семейства, что надето
    /// (лучнику — лук, магу — посох или мантия). Улучшением она будет не всегда — это решают бонусы, а не подгон.
    /// null — подходящих основ в каталоге нет.
    /// </summary>
    private static GridItem? MakeTargeted(ItemCatalog catalog, ItemEconomy economy, RpgState state, string rarity, int level, Random rng)
    {
        var wearers = LootAdvisor.Wearers(state).ToList();
        var who = wearers[0];
        var total = wearers.Count + 1; // герой — вдвое чаще
        var pick = rng.Next(total);
        if (pick >= 2) who = wearers[pick - 1];

        var arch = Progression.ArchetypeOf(who);
        who.Equipment.TryGetValue(nameof(EquipSlot.Hand1), out var hand1);
        who.Equipment.TryGetValue(nameof(EquipSlot.Hand2), out var hand2);
        var slots = new List<EquipSlot>
        {
            EquipSlot.Hand1, EquipSlot.Hand1, EquipSlot.Body, EquipSlot.Body, EquipSlot.Helmet, EquipSlot.Gloves,
            EquipSlot.Boots, EquipSlot.Cloak, EquipSlot.Belt, EquipSlot.Amulet, EquipSlot.Ring1,
        };
        var shieldUser = hand2 is not null ? economy.CategoryOf(hand2.Icon) == "shields"
            : hand1 is not { TwoHanded: true } && arch.Key is "tank" or "healer";
        if (shieldUser) slots.Add(EquipSlot.Hand2);
        var slot = slots[rng.Next(slots.Count)];

        var current = LootAdvisor.Current(who, slot);
        var familyOf = current?.Icon ?? (slot == EquipSlot.Hand1 ? arch.Weapon : slot == EquipSlot.Body ? arch.Armor : null);
        var family = familyOf is null ? null : LootAdvisor.Family(economy, familyOf, slot);
        var twoHanded = slot == EquipSlot.Hand1 ? current?.TwoHanded ?? catalog.Get(arch.Weapon)?.TwoHanded : null;

        bool Fits(ItemDef d) =>
            d.EquipSlot == slot
            && (slot != EquipSlot.Hand2 || economy.CategoryOf(d.Id) == "shields")
            && (family is null || LootAdvisor.Family(economy, d.Id, slot) == family)
            && (twoHanded is null || d.TwoHanded == twoHanded);

        var pool = GearPool(catalog, economy, Fits, rarity, level, rng);
        if (pool.Count == 0) pool = GearPool(catalog, economy, d => d.EquipSlot == slot, rarity, level, rng);
        return pool.Count == 0 ? null : BuildGear(PickDef(pool, economy, rng), economy, rarity, level, rng);
    }

    /// <summary>Лечебные и мановые зелья растут ступенями с уровнем (как в 5e: малое → высшее).</summary>
    private static GridItem MakeConsumable(int level, Random rng)
    {
        var tier = level < 5 ? 0 : level < 11 ? 1 : level < 17 ? 2 : 3;
        if (Genre.IsModern)
        {
            string[] mdHeal = { "2d4+2", "4d4+4", "8d4+8", "10d4+20" };
            string[] mdHealName = { "Бинты и обезболивающее", "Аптечка", "Армейская аптечка", "Набор военного медика" };
            string[] mdHealIcon = { "md_bandage", "md_medkit", "md_army_medkit", "md_army_medkit" };
            string[] focusName = { "Энергетик", "Таблетки кофеина", "Армейский стимулятор", "Армейский стимулятор (усиленный)" };
            int[] mdCost = { 8, 25, 70, 180 };
            var m = rng.NextDouble();
            if (m < 0.5)
                return new GridItem { Name = Tr(mdHealName[tier]), Icon = mdHealIcon[tier], W = tier == 0 ? 1 : 2, H = tier == 0 ? 1 : 2, Consumable = true, Quantity = 1 + (tier == 0 ? rng.Next(2) : 0), Value = mdCost[tier], Note = Lang.T($"Восстанавливает {mdHeal[tier]} ХП.", $"Restores {mdHeal[tier]} HP."), Level = level };
            if (m < 0.75)
                return new GridItem { Name = Tr(focusName[tier]), Icon = "md_energy", Consumable = true, Quantity = 1, Value = mdCost[tier], Note = Lang.T($"Восстанавливает {mdHeal[tier]} концентрации.", $"Restores {mdHeal[tier]} focus."), Level = level };
            if (m < 0.9)
                return new GridItem { Name = Tr("Противоядие"), Icon = "md_antidote", Consumable = true, Quantity = 1, Value = 8 + level, Note = Tr("Снимает отравление."), Level = level };
            return new GridItem { Name = Tr("Светошумовая граната"), Icon = "md_flashbang", Consumable = true, Quantity = 1, Value = 12 + level * 3, Note = Tr("Ослепление и оглушение на 1 раунд в зоне (спасбросок ТЕЛ DC 13)."), Level = level };
        }

        if (Genre.IsCyber)
        {
            string[] cyHeal = { "2d4+2", "4d4+4", "8d4+8", "10d4+20" };
            string[] cyHealName = { "Медстим", "Медстим+", "Военный медстим", "Медстим «Травма»" };
            string[] ramName = { "Стим ОЗУ", "Стим ОЗУ+", "Военный стим ОЗУ", "Стим ОЗУ «Овердрайв»" };
            int[] cost = { 8, 25, 70, 180 };
            var c = rng.NextDouble();
            if (c < 0.5)
                return new GridItem { Name = Tr(cyHealName[tier]), Icon = "cy_medstim", Consumable = true, Quantity = 1 + rng.Next(2), Value = cost[tier], Note = Lang.T($"Восстанавливает {cyHeal[tier]} ХП.", $"Restores {cyHeal[tier]} HP."), Level = level };
            if (c < 0.75)
                return new GridItem { Name = Tr(ramName[tier]), Icon = "cy_ram_stim", Consumable = true, Quantity = 1, Value = cost[tier], Note = Lang.T($"Восстанавливает {cyHeal[tier]} ОЗУ.", $"Restores {cyHeal[tier]} RAM."), Level = level };
            if (c < 0.9)
                return new GridItem { Name = Tr("Антитоксин"), Icon = "cy_antitox", Consumable = true, Quantity = 1, Value = 8 + level, Note = Tr("Снимает отравление токсином."), Level = level };
            return new GridItem { Name = Tr("Инъекция бронегеля"), Icon = "cy_shield_stim", Consumable = true, Quantity = 1, Value = 20 + level * 3, Note = Lang.T($"Щит на {5 + level * 2} очков на 3 раунда.", $"A shield of {5 + level * 2} points for 3 rounds."), Level = level };
        }

        string[] healName = { "Малое зелье лечения", "Зелье лечения", "Большое зелье лечения", "Высшее зелье лечения" };
        string[] heal = { "2d4+2", "4d4+4", "8d4+8", "10d4+20" };
        string[] manaName = { "Малое зелье маны", "Зелье маны", "Большое зелье маны", "Высшее зелье маны" };
        int[] price = { 8, 25, 70, 180 };
        var r = rng.NextDouble();
        if (r < 0.5)
            return new GridItem { Name = Tr(healName[tier]), Icon = "potion_red", Consumable = true, Quantity = 1 + rng.Next(2), Value = price[tier], Note = Lang.T($"Восстанавливает {heal[tier]} ХП.", $"Restores {heal[tier]} HP."), Level = level };
        if (r < 0.75)
            return new GridItem { Name = Tr(manaName[tier]), Icon = "potion_cyan", Consumable = true, Quantity = 1, Value = price[tier], Note = Lang.T($"Восстанавливает {heal[tier]} маны.", $"Restores {heal[tier]} mana."), Level = level };
        if (r < 0.9)
            return new GridItem { Name = Tr("Противоядие"), Icon = "potion_emerald", Consumable = true, Quantity = 1, Value = 8 + level, Note = Tr("Снимает отравление."), Level = level };
        return new GridItem { Name = Tr("Свиток защиты"), Icon = "scroll_blue", Consumable = true, Quantity = 1, Value = 20 + level * 3, Note = Lang.T($"Щит на {5 + level * 2} очков на 3 раунда.", $"A shield of {5 + level * 2} points for 3 rounds."), Level = level };
    }

    /// <summary>Источник добычи: поверженный враг или тайник (role = chest/hoard).</summary>
    public sealed record LootSource(int Level, string Role, string Kind, string Creature);

    /// <summary>
    /// Добыча за весь бой (или тайник). Слои:
    /// трофеи с тел и мелочь из карманов — на продажу; трофейное снаряжение — обычное, «чем дрались»;
    /// расходники и золото; и магическая находка (добротная и выше) — через копилку <see cref="RpgState.LootMeter"/>,
    /// поэтому её частота честна в среднем и не зависит от того, на сколько врагов разбит бой.
    /// Больше половины находок подбирается под слоты группы, остальное — что носил враг.
    /// </summary>
    public static LootResult RollEncounter(ItemCatalog catalog, ItemEconomy economy, RpgState state, IReadOnlyList<LootSource> sources,
        Random rng, double luck = 0)
    {
        var items = new List<GridItem>();
        var notes = new List<string>();
        if (sources.Count == 0) return new LootResult(items, 0, notes);
        var gold = 0;
        var pockets = 0;
        var worn = 0;

        foreach (var src in sources)
        {
            var level = Math.Max(1, src.Level);
            var role = src.Role;
            var kind = src.Kind;
            var isChest = role is "chest" or "hoard";
            var boss = role is "quest_boss" or "dungeon_boss" or "arc_boss";
            var who = string.IsNullOrWhiteSpace(src.Creature) ? PortraitCatalog.KindTitle(kind) : src.Creature.Trim();

            // Трофеи с тела — материалы для ремесленников и алхимиков.
            if (!isChest && Trophies.TryGetValue(kind, out var trophies))
            {
                foreach (var t in trophies)
                {
                    var chance = t.Chance * (role == "minion" ? 0.5 : 1) + (boss ? 0.2 : 0);
                    if (rng.NextDouble() >= chance || catalog.Get(t.Icon) is not { } def) continue;
                    var qty = rng.Next(t.Min, t.Max + 1) * (boss ? 2 : 1);
                    items.Add(new GridItem
                    {
                        Name = string.Format(Tr(t.Name), who), Icon = def.Id, W = def.W, H = def.H, Quantity = qty,
                        Value = Math.Max(1, (int)Math.Round(economy.BasePrice(def.Id) * PriceMult(level) * (boss ? 2 : 1))),
                        Note = Tr(t.Note), Level = level,
                    });
                }
            }

            // Мелочь из карманов: не больше двух за бой, чтобы окно обыска не превращалось в свалку.
            if (Pockets.TryGetValue(isChest ? "chest" : kind, out var pocketTable))
            {
                var (pc, pMin, pMax) = PocketRolls(role);
                if (rng.NextDouble() < pc)
                {
                    for (var n = rng.Next(pMin, pMax + 1); n > 0 && pockets < 2; n--, pockets++)
                    {
                        var p = PickPocket(pocketTable, rng);
                        var def = catalog.Get(p.Icon);
                        items.Add(new GridItem
                        {
                            Name = Tr(p.Name), Icon = def?.Id ?? "sack", W = def?.W ?? 1, H = def?.H ?? 1, Quantity = 1, Note = Tr(p.Note), Level = level,
                            Value = Math.Max(1, (int)Math.Round(p.Price * PriceMult(level))),
                        });
                    }
                }
            }

            // Трофейное снаряжение — обычное, без бонусов: новичку пригодится, потом — кузнецу.
            if (worn < 2 && rng.NextDouble() < WornChance(role) * (isChest ? 1 : GearChance(kind)))
            {
                var cats = (isChest ? new[] { "weapon", "armor", "shield", "jewelry" } : GearCats(kind)).Where(SlotCats.Contains).ToArray();
                if (cats.Length > 0)
                {
                    var item = MakeGear(catalog, economy, cats[rng.Next(cats.Length)], "common", level, rng);
                    if (!isChest)
                    {
                        item.Value = Math.Max(1, item.Value * 6 / 10);
                        item.Note = Tr("Трофейное: побывало в бою, но служит.");
                    }

                    items.Add(item);
                    worn++;
                }
            }

            // Расходники — у разумных и в тайниках.
            if ((kind == "humanoid" || isChest) && rng.NextDouble() < (boss || isChest ? 0.9 : role == "elite" ? 0.5 : 0.25))
            {
                items.Add(MakeConsumable(level, rng));
            }

            // Самоцветы в кладе драконов и боссов.
            if ((kind == "dragon" || role is "hoard" or "dungeon_boss" or "arc_boss") &&
                catalog.All.Where(d => Genre.IsFantasy ? d.Id.StartsWith("gem") : d.Cat == "valuable" && d.Slot is null).ToList() is { Count: > 0 } gems)
            {
                var g = gems[rng.Next(gems.Count)];
                items.Add(new GridItem { Name = g.Name, Icon = g.Id, Quantity = 1 + rng.Next(3), Value = (int)(economy.BasePrice(g.Id) * (1 + level / 5.0)), Level = level });
            }

            // Золото считается числом, не предметом.
            var (goldChance, goldMult) = GoldOf(isChest ? "humanoid" : kind);
            var dice = role switch { "minion" => 1, "elite" => 3, "quest_boss" => 10, "dungeon_boss" => 20, "arc_boss" => 30, "chest" => 8, "hoard" => 25, _ => 2 };
            if (rng.NextDouble() < goldChance)
            {
                var g = 0;
                for (var i = 0; i < dice; i++) g += rng.Next(1, 7);
                gold += (int)Math.Round(g * goldMult * (1 + (level - 1) / 4.0));
            }
        }

        // Магическая находка через копилку: вклад по роли и типу (звери почти не носят вещей), удача — +15% за единицу.
        double Weight(LootSource s) => MeterGain(s.Role) * (s.Role is "chest" or "hoard" ? 1 : GearChance(s.Kind));
        var meter = state.LootMeter + sources.Sum(Weight) * (1 + 0.15 * luck);
        var count = 0;

        // Волк не носит меч: если в бою нет тех, у кого бывают вещи, копилка ждёт следующей встречи с разумными или тайника.
        var carry = sources.Max(s => s.Role is "chest" or "hoard" ? 1 : GearChance(s.Kind));
        var eligible = rng.NextDouble() < carry;
        while (eligible && meter >= 1 && count < 4)
        {
            count++;
            meter -= 1;
        }

        // Остаток — шанс сейчас, а не обязательство: удача может прийти раньше, но тогда копилка уйдёт в минус.
        if (eligible && count < 4 && meter > 0 && rng.NextDouble() < meter * 0.6)
        {
            count++;
            meter -= 1;
        }

        for (var need = sources.Select(s => MagicGuarantee(s.Role)).DefaultIfEmpty(0).Max(); count < need; count++)
        {
            meter -= 1;
        }

        state.LootMeter = Math.Round(Math.Clamp(meter, -1, 2), 3);

        for (var i = 0; i < count; i++)
        {
            var src = PickSource(sources, Weight, rng);
            var level = Math.Max(1, src.Level);
            var rarity = RollRarity(src.Role, level, luck, rng);
            var item = rng.NextDouble() < 0.6 ? MakeTargeted(catalog, economy, state, rarity, level, rng) : null;
            if (item is null)
            {
                var cats = (src.Role is "chest" or "hoard" ? new[] { "weapon", "armor", "shield", "jewelry" } : GearCats(src.Kind)).Where(SlotCats.Contains).ToArray();
                item = MakeGear(catalog, economy, cats.Length > 0 ? cats[rng.Next(cats.Length)] : "jewelry", rarity, level, rng);
            }

            items.Add(item);
        }

        foreach (var it in items)
        {
            if (it.Rarity == "legendary") notes.Add(Lang.T($"легендарная вещь «{it.Name}»", $"legendary item \"{it.Name}\""));
        }

        return new LootResult(items, gold, notes);
    }

    private static Pocket PickPocket(Pocket[] table, Random rng)
    {
        var weights = table.Select(p => 1 / Math.Sqrt(p.Price)).ToArray();
        var roll = rng.NextDouble() * weights.Sum();
        for (var i = 0; i < table.Length; i++)
        {
            roll -= weights[i];
            if (roll <= 0) return table[i];
        }

        return table[^1];
    }

    /// <summary>С кого «упала» находка: чем сильнее враг и чем охотнее его тип носит вещи, тем вероятнее.</summary>
    private static LootSource PickSource(IReadOnlyList<LootSource> sources, Func<LootSource, double> weight, Random rng)
    {
        var total = sources.Sum(weight);
        if (total <= 0) return sources[0];
        var roll = rng.NextDouble() * total;
        foreach (var s in sources)
        {
            roll -= weight(s);
            if (roll <= 0) return s;
        }

        return sources[^1];
    }
}
