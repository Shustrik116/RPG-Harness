using System.Text.RegularExpressions;

namespace RPG_Harness.Services;

/// <summary>
/// Эффекты бойцов как механика, а не только текст. Эффект — строка «Оглушение (1 р.)»: название и, если есть,
/// остаток раундов. Харнес по словам названия понимает, что эффект делает (пропуск хода, преимущество,
/// КБ, урон во времени), сам тикает длительности в combat_turn и снимает боевые эффекты после боя и отдыха.
/// Слова-маркеры охватывают все три сеттинга: «поджог» и «горение», «благословение» и «тактическая синхронизация».
/// </summary>
public static class CombatEffects
{
    /// <summary>Хвост «(2 р.)», «(3 раунда)», «(1 ход)» — остаток длительности.</summary>
    private static readonly Regex DurationRx = new(
        @"\s*[\(\[]\s*(\d+)\s*(?:р\.?|раунд\w*|ход\w*|rounds?|rds?\.?|turns?|r)\s*[\)\]]\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Длительность без скобок: «оглушение, 2 раунда», «поджог — 3 р.».</summary>
    private static readonly Regex LooseDurationRx = new(
        @"[\s,:—–-]+(\d+)\s*(?:р\.|раунд\w*|rounds?|rds?\.)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Эффект длится до конца боя: снимается сам, когда последний противник выбыл.</summary>
    private static readonly Regex UntilCombatEndRx = new(@"до конца боя|until (?:the )?end of (?:the )?(?:combat|fight|battle)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Эффект до отдыха: снимается коротким или долгим отдыхом.</summary>
    private static readonly Regex UntilRestRx = new(@"до (?:короткого |долгого )?отдыха|до привала|до рассвета|until (?:a |the )?(?:short |long )?rest|until dawn", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string BaseName(string? effect)
    {
        var text = (effect ?? "").Trim();
        text = DurationRx.Replace(text, "");
        text = LooseDurationRx.Replace(text, "");
        return text.Trim();
    }

    public static int? Rounds(string? effect)
    {
        var text = (effect ?? "").Trim();
        var m = DurationRx.Match(text);
        if (!m.Success) m = LooseDurationRx.Match(text);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : null;
    }

    public static string WithRounds(string baseName, int? rounds) =>
        rounds is > 0 ? $"{baseName} ({rounds} {Lang.T("р.", "rd.")})" : baseName;

    private static bool Is(string text, params string[] stems) =>
        stems.Any(s => Regex.IsMatch(text, @"(?<![\p{L}])" + Regex.Escape(s), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

    // ===== Что делает эффект =====

    private static readonly string[] SkipStems =
        { "оглуш", "усыпл", "сон", "спит", "паралич", "парализ", "замороз", "заморож", "окамен", "без сознания", "оцепен",
          "короткое замыкание", "взлом нейросистем", "контузи", "удар током", "слом",
          "stun", "sleep", "asleep", "paraly", "frozen", "freez", "petrif", "unconscious", "short circuit", "neural hack", "concuss",
          "electric shock", "break", "knocked out", "shutdown", "shut down" };

    private static readonly string[] HelplessStems =
        { "ослеп", "сбой оптики", "опутан", "захват", "захвач", "наручник", "сбит с ног", "лежит", "повален",
          "blind", "optics glitch", "entangl", "grappl", "restrain", "handcuff", "prone", "knocked down" };

    private static readonly string[] AttackDisStems =
        { "ослеп", "сбой оптики", "вспышк", "страх", "испуг", "паник", "отравл", "токсин", "проклят", "вирус", "головокруж",
          "слезоточ", "контузи", "опутан", "захвач", "наручник",
          "blind", "optics glitch", "flash", "fear", "fright", "panic", "poison", "toxin", "curse", "virus", "dizz", "tear gas", "concuss",
          "entangl", "grappl", "handcuff" };

    private static readonly string[] CheckDisStems = { "отравл", "токсин", "проклят", "вирус", "головокруж", "слезоточ", "истощ",
        "poison", "toxin", "curse", "virus", "dizz", "tear gas", "exhaust" };

    private static readonly string[] AttackAdvStems = { "невидим", "камуфляж", "точност", "наведени", "прицелив", "скрыт",
        "invisib", "camo", "precision", "targeting", "aiming", "hidden" };

    private static readonly string[] GrantDisStems = { "невидим", "камуфляж", "уклонени", "invisib", "camo", "evasion" };

    /// <summary>Эффекты, которые спадают, как только носитель атакует (невидимость, разовая точность).</summary>
    private static readonly string[] OnAttackStems = { "невидим", "камуфляж", "точност", "наведени", "прицелив", "скрыт",
        "invisib", "camo", "precision", "targeting", "aiming", "hidden" };

    private static readonly string[] BlessStems = { "благослов", "синхронизац", "координац", "вдохнов", "bless", "sync", "tactical coordination", "inspir" };

    private static readonly string[] VulnerableStems = { "уязвим", "метка цели", "слом", "vulnerab", "target mark", "break" };

    private static readonly string[] ResistPhysicalStems = { "стойкост", "бронированн", "fortitude", "armored skin", "steadfast" };

    private static readonly string[] PhysicalTypes =
        { "рубящ", "колющ", "дробящ", "кинетич", "огнестрел", "ударн", "режущ", "физич",
          "slash", "pierc", "bludgeon", "kinetic", "ballistic", "blunt", "physical" };

    /// <summary>Сводка механики набора эффектов одного бойца.</summary>
    public sealed record Mods(int Attack, int Damage, int Ac, bool Adv, bool Dis, bool GrantAdv, bool GrantDis,
        bool Vulnerable, bool ResistPhysical, bool Bless, bool SkipsTurn, bool CheckDis, List<string> Why);

    public static Mods Read(IEnumerable<string>? effects)
    {
        int atk = 0, dmg = 0, ac = 0;
        bool adv = false, dis = false, grantAdv = false, grantDis = false, vuln = false, resist = false, bless = false, skip = false, checkDis = false;
        var why = new List<string>();
        foreach (var raw in effects ?? Enumerable.Empty<string>())
        {
            var e = BaseName(raw).ToLowerInvariant();
            if (e.Length == 0) continue;
            if (Is(e, SkipStems)) { skip = true; grantAdv = true; }
            if (Is(e, HelplessStems)) grantAdv = true;
            if (Is(e, AttackDisStems)) dis = true;
            if (Is(e, CheckDisStems)) checkDis = true;
            if (Is(e, AttackAdvStems)) adv = true;
            if (Is(e, GrantDisStems)) grantDis = true;
            if (Is(e, BlessStems)) bless = true;
            if (Is(e, VulnerableStems)) vuln = true;
            if (Is(e, ResistPhysicalStems)) resist = true;
            if (Is(e, "немощ", "feeble")) { atk -= 2; dmg -= 2; }
            if (Is(e, "подавление огн", "suppress")) atk -= 2;
            if (Is(e, "ярост", "боевой стим", "адреналин", "rage", "combat stim", "adrenaline")) { atk += 2; dmg += 2; ac -= 2; }
            if (Is(e, "снижение защит", "снижена защит", "пробит", "замедл", "перегрузк", "lowered defense", "armor breach", "slow", "overload")) ac -= 2;
            if (Is(e, "ускорен", "рефлекс-бустер", "прикрыти", "haste", "reflex booster", "covering")) ac += 2;
            if (Is(e, "магический доспех", "магическая броня", "силовой экран", "бронеплит", "mage armor", "force screen", "armor plate")) ac += 3;
            if (e is "защита" or "в защите" or "defend" or "defending" or "defense" || Is(e, "оборон", "защитная стойка", "глухая защита", "defensive stance", "full defense")) ac += 4;
            if (Is(e, "кислот", "коррози", "acid", "corrosion")) ac -= 1;
            why.Add(e);
        }

        return new Mods(atk, dmg, ac, adv, dis, grantAdv, grantDis, vuln, resist, bless, skip, checkDis, why);
    }

    public static bool SkipsTurn(string? effect) => Is(BaseName(effect).ToLowerInvariant(), SkipStems);

    public static bool IsPhysical(string? damageType) => Is((damageType ?? "").ToLowerInvariant(), PhysicalTypes);

    /// <summary>Урон или лечение в начале хода носителя: кость, тип и лечит ли.</summary>
    public static (string Dice, string Type, bool Heal)? Periodic(string? effect)
    {
        var e = BaseName(effect).ToLowerInvariant();
        if (Is(e, "нанорегенерац", "регенерац", "второе дыхание", "nano-regen", "regenerat", "second wind")) return ("1d6", Lang.T("лечение", "healing"), true);
        if (Is(e, "поджог", "горит", "горени", "пылает", "burn", "on fire", "ablaze")) return ("1d6", Lang.T("огнём", Genre.Pick("fire", "thermal", "fire")), false);
        if (Is(e, "кровотеч", "bleed")) return ("1d6", Lang.T("от кровотечения", "bleeding"), false);
        if (Is(e, "отравл", "токсин", "poison", "toxin")) return ("1d4", Lang.IsEn ? Genre.Pick("poison", "toxin", "chemical") : Genre.Pick("ядом", "токсином", "отравой"), false);
        if (Is(e, "кислот", "коррози", "acid", "corrosion")) return ("1d4", Lang.IsEn ? Genre.Pick("acid", "corrosion", "acid") : Genre.Pick("кислотой", "коррозией", "кислотой"), false);
        if (Is(e, "некрот", "порча", "радиац", "necrotic", "blight", "radiation")) return ("1d6", Lang.IsEn ? Genre.Pick("necrotic", "radiation", "radiation") : Genre.Pick("некротикой", "радиацией", "радиацией"), false);
        return null;
    }

    /// <summary>Типичная длительность стандартного эффекта в бою (раунды); null — до конца боя или неизвестно.</summary>
    public static int? DefaultRounds(string? effect)
    {
        var e = BaseName(effect).ToLowerInvariant();
        if (Is(e, "магический доспех", "силовой экран", "бронеплит", "ярост", "боевой стим", "адреналин", "стойкост", "бронированн",
                "mage armor", "force screen", "armor plate", "rage", "combat stim", "adrenaline", "fortitude", "armored skin")) return null;
        if (Is(e, "оглуш", "паралич", "парализ", "замороз", "заморож", "короткое замыкание", "контузи", "удар током", "слом", "окамен",
                "stun", "paraly", "frozen", "freez", "short circuit", "concuss", "electric shock", "break", "petrif")) return 1;
        if (Is(e, "уязвим", "метка цели", "кислот", "коррози", "некрот", "порча", "радиац",
                "vulnerab", "target mark", "acid", "corrosion", "necrotic", "blight", "radiation")) return 2;
        if (Is(e, "сон", "спит", "усыпл", "страх", "испуг", "паник", "очаров", "замешат", "сбой навиг", "немощ", "снижение защит", "пробит",
                "ослеп", "сбой оптики", "проклят", "вирус", "головокруж", "замедл", "перегрузк", "поджог", "горит", "горени", "кровотеч",
                "отравл", "токсин", "благослов", "синхронизац", "координац", "ускорен", "рефлекс-бустер", "регенерац", "второе дыхание",
                "опутан", "захват", "немот", "блокировк",
                "sleep", "asleep", "fear", "fright", "panic", "charm", "confus", "navigation glitch", "feeble", "lowered defense", "armor breach",
                "blind", "optics glitch", "curse", "virus", "dizz", "slow", "overload", "burn", "bleed", "poison", "toxin", "bless", "sync",
                "coordinat", "haste", "reflex booster", "regenerat", "second wind", "entangl", "grappl", "silence", "implant lock", "suppress")) return 3;
        return null;
    }

    // ===== Списки эффектов =====

    /// <summary>
    /// Добавить эффект или обновить длительность такого же: эффекты одного типа не складываются,
    /// повторное наложение обновляет срок. Возвращает итоговую строку эффекта.
    /// </summary>
    public static string AddOrRefresh(List<string> list, string effect, int? rounds = null, bool inCombat = false)
    {
        var name = BaseName(effect);
        if (name.Length == 0) return "";
        var left = rounds is > 0 ? rounds : Rounds(effect);
        if (left is null && inCombat && !UntilRestRx.IsMatch(name) && !UntilCombatEndRx.IsMatch(name))
        {
            left = DefaultRounds(name);
        }

        list.RemoveAll(x => BaseName(x).Equals(name, StringComparison.OrdinalIgnoreCase));
        var text = WithRounds(name, left);
        list.Add(text);
        return text;
    }

    /// <summary>Снять эффект по названию: сначала точное совпадение основы, затем по началу слова.</summary>
    public static int Remove(List<string> list, string? name)
    {
        var wanted = BaseName(name);
        if (wanted.Length == 0) return 0;
        var removed = list.RemoveAll(x => BaseName(x).Equals(wanted, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            removed = list.RemoveAll(x => BaseName(x).StartsWith(wanted, StringComparison.OrdinalIgnoreCase) ||
                                          wanted.StartsWith(BaseName(x), StringComparison.OrdinalIgnoreCase) && BaseName(x).Length >= 4);
        }

        return removed;
    }

    /// <summary>Конец хода носителя: длительности −1, истёкшие снимаются. Возвращает названия истёкших.</summary>
    public static List<string> Tick(List<string> list)
    {
        var expired = new List<string>();
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (Rounds(list[i]) is not int left) continue;
            var name = BaseName(list[i]);
            if (left <= 1)
            {
                expired.Insert(0, name);
                list.RemoveAt(i);
            }
            else
            {
                list[i] = WithRounds(name, left - 1);
            }
        }

        return expired;
    }

    /// <summary>Атаковавший теряет невидимость и разовую точность.</summary>
    public static List<string> SpendOnAttack(List<string> list)
    {
        var spent = list.Where(x => Is(BaseName(x).ToLowerInvariant(), OnAttackStems)).Select(BaseName).ToList();
        list.RemoveAll(x => Is(BaseName(x).ToLowerInvariant(), OnAttackStems));
        return spent;
    }

    /// <summary>Бой окончен: снимаются эффекты на раунды и «до конца боя». Возвращает снятые.</summary>
    public static List<string> ClearAfterCombat(List<string> list)
    {
        var gone = list.Where(x => Rounds(x) is not null || UntilCombatEndRx.IsMatch(x) || Is(BaseName(x).ToLowerInvariant(), "слом", "break"))
            .Select(BaseName).ToList();
        list.RemoveAll(x => Rounds(x) is not null || UntilCombatEndRx.IsMatch(x) || Is(BaseName(x).ToLowerInvariant(), "слом", "break"));
        return gone;
    }

    /// <summary>Отдых: снимаются боевые эффекты и всё «до отдыха». Возвращает снятые.</summary>
    public static List<string> ClearAfterRest(List<string> list)
    {
        var gone = ClearAfterCombat(list);
        gone.AddRange(list.Where(x => UntilRestRx.IsMatch(x)).Select(BaseName));
        list.RemoveAll(x => UntilRestRx.IsMatch(x));
        return gone;
    }
}
