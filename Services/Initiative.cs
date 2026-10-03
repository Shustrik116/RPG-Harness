namespace RPG_Harness.Services;

/// <summary>
/// Очередь хода боя: единое место, где инициатива из <see cref="RpgState.Initiative"/> превращается
/// в места, строки для модели и словарь для окна боя. Одни и те же числа видят модель (сводка состояния
/// и инструмент initiative), игрок (бейджи в окне боя) и код, поэтому порядок у всех совпадает.
/// </summary>
public static class Initiative
{
    /// <summary>Как зовут героя, если мастер записал его без имени (в журнале боя такое встречается).</summary>
    public static string HeroAlias => Lang.T("Герой", "Hero");

    /// <summary>
    /// Места в порядке хода: по убыванию броска, ничья — тот, кто бросил раньше (стабильная сортировка,
    /// порядок в списке = порядок бросания).
    /// </summary>
    public static List<(string Name, int Total)> Seats(IReadOnlyList<InitiativeSeat> rolls) =>
        rolls.Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .OrderByDescending(r => r.Total)
            .Select(r => (r.Name.Trim(), r.Total))
            .ToList();

    /// <summary>Имя участника → номер по порядку хода (1 — ходит первым).</summary>
    public static Dictionary<string, int> Order(IReadOnlyList<InitiativeSeat> rolls)
    {
        var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var seat = 0;
        foreach (var (name, _) in Seats(rolls))
        {
            order[name] = ++seat;
        }

        return order;
    }

    /// <summary>Одна строка для сводки состояния и результата броска: «1. Герой 21 → 2. Гоблин 17».</summary>
    public static string Line(IReadOnlyList<InitiativeSeat> rolls)
    {
        var seats = Seats(rolls);
        return seats.Count == 0
            ? ""
            : string.Join(" → ", seats.Select((s, i) => $"{i + 1}. {s.Name} {s.Total}"));
    }

    /// <summary>Очередь построчно для результата инструмента: «1. Герой — 21».</summary>
    public static string Numbered(IReadOnlyList<InitiativeSeat> rolls)
    {
        var seats = Seats(rolls);
        return seats.Count == 0
            ? Lang.T("(пусто)", "(empty)")
            : string.Join("\n", seats.Select((s, i) => $"{i + 1}. {s.Name} — {s.Total}"));
    }

    /// <summary>
    /// Участника можно найти в очереди под этим именем. heroName — имя героя: его очередь ведёт то под
    /// этим именем, то под «Герой» (так записывает журнал боя), и оба варианта должны сходиться.
    /// </summary>
    public static bool HasSeat(IReadOnlyList<InitiativeSeat> rolls, string? name, string? heroName = null)
    {
        var wanted = (name ?? "").Trim();
        if (wanted.Length == 0)
        {
            return false;
        }

        if (rolls.Any(r => (r.Name ?? "").Trim().Equals(wanted, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var isHero = heroName is { Length: > 0 } && wanted.Equals(heroName.Trim(), StringComparison.OrdinalIgnoreCase);
        return isHero && rolls.Any(r => (r.Name ?? "").Trim().Equals(HeroAlias, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Кто из живых участников боя не попал в очередь: герой, спутники и противники с ХП больше нуля.
    /// Пусто, если бой не идёт — тогда предупреждать некого.
    /// </summary>
    public static List<string> Missing(RpgState state)
    {
        if (!state.InCombat())
        {
            return new List<string>();
        }

        var fighters = new List<string> { state.Character.Name };
        fighters.AddRange(state.Party.Where(p => p.HpCurrent > 0).Select(p => p.Name));
        fighters.AddRange(state.Adversaries.Where(a => a.InFight).Select(a => a.Name));

        return fighters
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Where(n => !HasSeat(state.Initiative, n, state.Character.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}