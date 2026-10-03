using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RPG_Harness.Services;

/// <summary>
/// Превращает вызовы инструментов мастера в записи боевого журнала: бросок, урон, лечение,
/// щит, наложенный или снятый эффект, использованный предмет, выбывший противник, добыча.
/// Работает после успешного выполнения инструмента, поэтому читает уже итоговые аргументы.
/// Записи делаются «человеческими»: кто → на кого → сколько, а у бросков показывается,
/// из чего сложился результат («15 + 3 = 18»), а не только итоговое число.
/// </summary>
public static class BattleLogFeed
{
    /// <summary>Метка цели для карточки героя (однозначно отличается от имён противников и спутников).</summary>
    public const string HeroTarget = "@hero";

    /// <summary>
    /// Очередь хода по журналу боя: имя участника → номер (1 — ходит первым).
    /// У каждого участника берётся последний бросок инициативы, порядок — по сумме броска по убыванию
    /// (ничья — кто бросил раньше). Пустой словарь, если инициативу ещё не бросали.
    /// </summary>
    public static Dictionary<string, int> InitiativeOrder(IReadOnlyList<BattleLogEntry> entries)
    {
        var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (entries.Count == 0)
        {
            return order;
        }

        var rolls = new Dictionary<string, (int Total, long Id)>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            if (e.Kind != "initiative" || string.IsNullOrWhiteSpace(e.Actor))
            {
                continue;
            }

            // Повторный бросок за того же участника перекрывает прежний.
            if (!rolls.TryGetValue(e.Actor, out var previous) || e.Id > previous.Id)
            {
                rolls[e.Actor] = (e.Amount ?? 0, e.Id);
            }
        }

        var seat = 0;
        foreach (var roll in rolls.OrderByDescending(r => r.Value.Total).ThenBy(r => r.Value.Id))
        {
            order[roll.Key] = ++seat;
        }

        return order;
    }

    /// <summary>Разбор результата roll_dice: «🎲 1d20+3 (причина): 1d20[15] + 3 = 18».</summary>
    private static readonly Regex RollRe = new(
        @"^🎲\s*(?<expr>.*?)(?:\s*\((?<why>[^()]*)\))?:\s*(?<parts>.+?)\s*=\s*(?<total>-?\d+)\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Часть формулы броска за один проход: либо кубик, либо плоский модификатор («+ 4», «− 2»).
    /// Один проход нужен, чтобы подписи не попали под повторный разбор.
    /// </summary>
    private static readonly Regex PartRe = new(
        @"(?<die>(?<count>\d*)d(?<sides>\d+|%)(?:k[hl]?|d[hl]?)?\d*\[(?<inner>[^\]]*)\])" +
        @"|(?<sign>[+−-])\s*(?<value>\d+)(?![\w])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TotalRe = new(@"=\s*(-?\d+)\s*$", RegexOptions.Compiled);
    private static readonly Regex BracketRe = new(@"\[([^\]]*)\]", RegexOptions.Compiled);
    private static readonly Regex NumberRe = new(@"\d+", RegexOptions.Compiled);
    private static readonly Regex TurnRe = new(@"(?:Цель|Target):\s*(.+?)\.\s*(?:Действие|Action):\s*(.+?)\.", RegexOptions.Compiled);
    private static readonly Regex InitiativeRe = new(@"инициатив|initiative", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AttackRe = new(@"атак|удар|выстрел|бьёт|бьет|замах|attack|strike|shot|hit|swing", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SaveRe = new(@"спасброс|спасение|saving throw|save", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DamageRe = new(@"урон|ущерб|damage", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Обработать один выполненный вызов инструмента. inBattle — в чате есть живые противники.</summary>
    public static void Feed(BattleLog log, string chatId, ToolCall call, bool inBattle)
    {
        if (call.Ok != true)
        {
            return;
        }

        // Инструменты, которые сами бросают кости (атака, тик эффектов, инициатива, проверка), передают
        // готовые события: журнал показывает ровно то, что посчитал харнес, без разбора текста.
        foreach (var ev in CombatEvents.Drain(chatId))
        {
            var entry = log.Add(chatId, ev.Kind, ev.Icon, ev.Text, target: ev.Target, amount: ev.Amount, label: ev.Label,
                crit: ev.Crit, fail: ev.Fail, actor: ev.Actor, subject: ev.Subject, value: ev.Value, detail: ev.Detail,
                verdict: ev.Verdict, verdictBad: ev.VerdictBad);
            if (ev.Damage is int dmg)
            {
                log.AppendDamage(chatId, entry.Id, dmg);
            }
        }

        var a = Args(call.Arguments);
        switch (call.Name)
        {
            case "roll_dice":
                if (inBattle)
                {
                    FeedRoll(log, chatId, a, call.Result);
                }
                break;

            case "resolve_death_save":
                if (inBattle && call.Result is { Length: > 0 } deathResult)
                {
                    var roll = Regex.Match(deathResult, @"d20\[(\d+)\]");
                    var amount = roll.Success && int.TryParse(roll.Groups[1].Value, out var value) ? value : (int?)null;
                    var bad = Regex.IsMatch(deathResult, @"—\s*(?:провал|натуральная 1|failure|natural 1)", RegexOptions.IgnoreCase) ||
                              deathResult.Contains("мёртв", StringComparison.OrdinalIgnoreCase) || deathResult.Contains("is dead", StringComparison.OrdinalIgnoreCase);
                    log.Add(chatId, "save", "☠", deathResult, amount: amount, label: Lang.T("Спасбросок от смерти", "Death save"),
                        fail: amount == 1, crit: amount == 20, actor: S(a, "target"), value: amount?.ToString(),
                        verdict: bad ? Lang.T("провал", "failure") : Lang.T("успех", "success"), verdictBad: bad);
                }
                break;

            case "update_adversary":
                FeedAdversary(log, chatId, a, call.Result);
                break;

            case "update_character":
                if (inBattle)
                {
                    FeedHero(log, chatId, a);
                }
                break;

            case "update_party_member":
                if (inBattle)
                {
                    FeedParty(log, chatId, a);
                }
                break;

            case "consume_item":
                if (inBattle)
                {
                    FeedItem(log, chatId, a);
                }
                break;

            case "update_battle_loot":
                FeedLoot(log, chatId, a);
                break;
        }
    }

    /// <summary>Короткая строка хода игрока из служебного запроса боевого окна.</summary>
    public static string TurnLine(string? request)
    {
        if ((request ?? "").Contains("ПРОДОЛЖЕНИЕ БОЯ", StringComparison.Ordinal) || (request ?? "").Contains("CONTINUE COMBAT", StringComparison.Ordinal))
        {
            return Lang.T("Ход героя пропущен — бой идёт сам", "The hero's turn is skipped — the fight runs on its own");
        }

        var m = TurnRe.Match(request ?? "");
        if (!m.Success)
        {
            return Lang.T("Ход игрока", "Player's turn");
        }

        var target = m.Groups[1].Value.Trim();
        var action = m.Groups[2].Value.Trim();
        return target.Length > 0 && !target.StartsWith("не выбрана", StringComparison.OrdinalIgnoreCase) && !target.StartsWith("not chosen", StringComparison.OrdinalIgnoreCase)
            ? Lang.T("Ход игрока: ", "Player's turn: ") + $"{action} → {target}"
            : Lang.T("Ход игрока: ", "Player's turn: ") + action;
    }

    // ===== Броски =====

    private static void FeedRoll(BattleLog log, string chatId, JsonObject a, string? result)
    {
        if (string.IsNullOrWhiteSpace(result))
        {
            return;
        }

        var expr = S(a, "expression") ?? "";
        var reason = S(a, "reason") ?? "";
        var total = ParseTotal(result);
        var (crit, fail) = ParseCrit(expr, result);

        var check = (S(a, "check") ?? "").ToLowerInvariant();
        var attacker = S(a, "attacker");
        var target = S(a, "target");
        var dc = TryInt(S(a, "dc"), out var dcValue) ? dcValue : (int?)null;

        // Тип броска: явный check из аргументов, иначе догадка по формулировке причины.
        var initiative = check == "initiative" || InitiativeRe.IsMatch(reason);
        var damageRoll = check == "damage" || DamageRe.IsMatch(reason);
        var isAttack = !initiative && !damageRoll && (check == "attack" || AttackRe.IsMatch(reason));
        var isSave = !initiative && !isAttack && (check == "save" || SaveRe.IsMatch(reason));

        // Урон после успешной атаки дописываем в строку самой атаки: игрок видит попадание и урон рядом,
        // а не отдельной строкой ниже. Мастер часто бросает все атаки пачкой, а урон — следом, поэтому
        // ищем атаку именно этого действующего, а не просто последнюю.
        if (damageRoll && total is int damage && TryAttachDamage(log, chatId, attacker, reason, damage))
        {
            return;
        }

        var kind = initiative ? "initiative" : isAttack ? "attack" : isSave ? "save" : damageRoll ? "damage" : "roll";
        var icon = initiative ? "⚑" : isAttack ? "⚔" : isSave ? "✦" : "🎲";

        // Вердикт — сравнение итога с классом брони (атака) или СЛ (спасбросок).
        // Без числа показываем только очевидное: натуральная 20 и натуральная 1.
        string? verdict = null;
        var verdictBad = false;
        if (dc is int need && total is int got)
        {
            if (isAttack)
            {
                verdict = crit ? Lang.T("крит!", "crit!") : fail ? Lang.T("промах", "miss") : got >= need ? Lang.T("попадание", "hit") : Lang.T("промах", "miss");
                verdictBad = !crit && (fail || got < need);
            }
            else if (isSave)
            {
                verdict = got >= need ? Lang.T("успех", "success") : Lang.T("провал", "failure");
                verdictBad = got < need;
            }
        }
        else if (isAttack && (crit || fail))
        {
            verdict = crit ? Lang.T("крит!", "crit!") : Lang.T("промах", "miss");
            verdictBad = fail;
        }

        var title = reason.Length > 0 ? reason.Trim() : expr;
        if (initiative && !InitiativeRe.IsMatch(title))
        {
            title = Lang.T("Инициатива: ", "Initiative: ") + title;
        }

        // «Кто → по кому» показываем, только если известны оба участника; иначе — обычная строка.
        var hasPair = attacker is { Length: > 0 } && target is { Length: > 0 };

        // Инициатива — строка про одного участника: цели у неё нет, но имя бросающего нужно,
        // чтобы окно боя построило очередь хода по карточкам. Без цели имя уходило в Text.
        var soloActor = !hasPair && initiative && attacker is { Length: > 0 };

        log.Add(chatId,
            kind,
            icon,
            hasPair ? "" : soloActor ? Lang.T("инициатива", "initiative") : title,
            amount: total,
            label: title,
            crit: crit,
            fail: fail,
            actor: hasPair || soloActor ? attacker : null,
            subject: hasPair ? target : null,
            value: total?.ToString(),
            detail: RollDetail(result, dc, isAttack),
            verdict: verdict,
            verdictBad: verdictBad);
    }

    /// <summary>
    /// Приписать урон к подходящей строке атаки. Возвращает false, если подходящей атаки нет —
    /// тогда урон показывается отдельной строкой (например, урон от падения или ловушки).
    /// </summary>
    private static bool TryAttachDamage(BattleLog log, string chatId, string? attacker, string reason, int damage)
    {
        var entries = log.Get(chatId);
        var window = Math.Max(0, entries.Count - 12);
        long? fallback = null;

        for (var i = entries.Count - 1; i >= window; i--)
        {
            var e = entries[i];
            if (e.Kind != "attack")
            {
                continue;
            }

            // Урон у этой атаки уже показан — ищем другую строку.
            if (e.Damage is not null)
            {
                continue;
            }

            if (!SameActor(e.Actor, attacker, reason))
            {
                continue;
            }

            // Подтверждённое попадание — приписываем сразу.
            if (e.Verdict is "попадание" or "крит!" or "hit" or "crit!")
            {
                return log.AppendDamage(chatId, e.Id, damage);
            }

            // Вердикта нет (мастер не назвал КБ): урон обычно бросают только после попадания,
            // поэтому такую атаку держим в запасе и используем, если точного попадания не нашлось.
            if (e.Verdict is null && fallback is null)
            {
                fallback = e.Id;
            }
        }

        return fallback is long id && log.AppendDamage(chatId, id, damage);
    }

    /// <summary>
    /// Тот ли это действующий. Если имя передано аргументом — сравниваем строки. Иначе сверяем
    /// слова из имени атаки со словами причины броска по началу слова: мастер пишет причину в косвенном
    /// падеже («Урон гоблина-разбойника 3 по герою» → атака «Гоблин-разбойник 3»).
    /// </summary>
    private static bool SameActor(string? entryActor, string? arg, string reason)
    {
        if (entryActor is not { Length: > 0 })
        {
            return false;
        }

        if (arg is { Length: > 0 })
        {
            return entryActor.Contains(arg, StringComparison.OrdinalIgnoreCase)
                || arg.Contains(entryActor, StringComparison.OrdinalIgnoreCase);
        }

        var actorWords = Words(entryActor);
        if (actorWords.Count == 0)
        {
            return false;
        }

        var reasonWords = Words(reason);
        return actorWords.All(w => reasonWords.Any(r => SameStem(w, r)));
    }

    /// <summary>Слова имени без знаков препинания: «Гоблин-разбойник 3» → [гоблин, разбойник, 3].</summary>
    private static List<string> Words(string text) =>
        text.Split(
                [' ', '-', '—', ',', '.', '(', ')', '«', '»', '!', '?', ':', ';', '№', '"'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    /// <summary>Одно и то же слово в разных падежах: «гоблин» ↔ «гоблина» — сверяем начало слова.</summary>
    private static bool SameStem(string a, string b)
    {
        if (a.Equals(b, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var len = Math.Min(4, Math.Min(a.Length, b.Length));
        return len >= 3 && a.AsSpan(0, len).Equals(b.AsSpan(0, len), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Расшифровка броска для человека: «d20 19 + бонус 4 = 23 против КБ 15».
    /// Каждая часть подписана, чтобы было видно, где выпавший кубик, а где модификатор.
    /// Бросок без модификаторов («d20 18») итогом не дублируем.
    /// </summary>
    private static string? RollDetail(string? result, int? dc, bool isAttack)
    {
        var m = RollRe.Match(result ?? "");
        var math = "";
        if (m.Success)
        {
            var humanized = HumanizeParts(m.Groups["parts"].Value.Trim());
            var total = m.Groups["total"].Value;
            math = NeedsTotal(humanized) ? $"{humanized} = {total}" : humanized;
        }

        if (dc is not int need)
        {
            return math.Length > 0 ? math : null;
        }

        var needLabel = isAttack ? Lang.T("КБ", "AC") : Lang.T("СЛ", "DC");
        return math.Length > 0 ? $"{math} {Lang.T("против", "vs")} {needLabel} {need}" : $"{needLabel} {need}";
    }

    /// <summary>
    /// Нужно ли показывать итог отдельно. Итог дублировал бы число только у одиночного броска
    /// («d20 18»), а у суммы («d20 19 + бонус 4», «2d6 (3 + 4)») он складывает слагаемые и полезен.
    /// </summary>
    private static bool NeedsTotal(string humanized) => humanized.Contains(" + ", StringComparison.Ordinal);

    /// <summary>
    /// Разбирает формулу броска на подписанные части за один проход:
    /// «1d20[19] + 4» → «d20 19 + бонус 4», «2d6[1, 2] + 2» → «2d6 (1 + 2) + бонус 2».
    /// Благодаря подписям видно, где выпавший кубик, а где модификатор от характеристик и предметов.
    /// </summary>
    private static string HumanizeParts(string parts) => PartRe.Replace(parts, m =>
    {
        if (!m.Groups["die"].Success)
        {
            return (m.Groups["sign"].Value == "+" ? Lang.T("+ бонус ", "+ bonus ") : Lang.T("− штраф ", "− penalty ")) + m.Groups["value"].Value;
        }

        var count = m.Groups["count"].Value;
        var sides = m.Groups["sides"].Value;
        // Единицу не пишем: «d20 19» читается лучше, чем «1d20 19».
        var notation = (count.Length == 0 || count == "1" ? "" : count) + "d" + sides;
        var inner = HumanizeDieInner(m.Groups["inner"].Value.Trim());
        if (inner.Length == 0)
        {
            return notation;
        }

        // Несколько значений берём в скобки, чтобы «+ бонус N» не сливался с ними.
        var multi = inner.Contains(" + ", StringComparison.Ordinal) || inner.Contains(" и ", StringComparison.Ordinal) || inner.Contains(" and ", StringComparison.Ordinal);
        return multi ? $"{notation} ({inner})" : $"{notation} {inner}";
    }).Trim();

    /// <summary>Расшифровка выпавшего на кубиках: «4, 5» → «4 + 5», «7/16→16» → «из 7 и 16 взят 16».</summary>
    private static string HumanizeDieInner(string inner)
    {
        if (inner.Length == 0)
        {
            return "";
        }

        // 4d6kh3[6, 3, 5, 2 ⇒ 6+5+3] — оставлены лучшие кубики.
        var keep = inner.IndexOf('⇒');
        if (keep >= 0)
        {
            return inner[(keep + 1)..].Trim();
        }

        // 1d20[7/16→16] — преимущество или помеха: брошено дважды, взят лучший/худший.
        var slash = inner.IndexOf('/');
        var arrow = inner.IndexOf('→');
        if (slash >= 0 && arrow > slash)
        {
            var first = inner[..slash].Trim();
            var second = inner[(slash + 1)..arrow].Trim();
            var picked = inner[(arrow + 1)..].Trim();
            return Lang.T($"из {first} и {second} взят {picked}", $"of {first} and {second} took {picked}");
        }

        // 2d6[4, 5] — сумма нескольких костей.
        return inner.Contains(',')
            ? string.Join(" + ", inner.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            : inner;
    }

    private static int? ParseTotal(string? result)
    {
        var m = TotalRe.Match(result ?? "");
        return m.Success && int.TryParse(m.Groups[1].Value, out var value) ? value : null;
    }

    /// <summary>Натуральная 20 / натуральная 1 на броске d20 (по значению оставленного кубика).</summary>
    private static (bool Crit, bool Fail) ParseCrit(string expr, string? result)
    {
        if (!expr.Contains("d20", StringComparison.OrdinalIgnoreCase))
        {
            return (false, false);
        }

        var bracket = BracketRe.Match(result ?? "");
        if (!bracket.Success)
        {
            return (false, false);
        }

        var numbers = NumberRe.Matches(bracket.Groups[1].Value);
        if (numbers.Count == 0)
        {
            return (false, false);
        }

        var face = int.Parse(numbers[^1].Value);
        return (face == 20, face == 1);
    }

    // ===== Противники =====

    private static void FeedAdversary(BattleLog log, string chatId, JsonObject a, string? result)
    {
        var name = S(a, "name") ?? "";
        if (name.Length == 0)
        {
            return;
        }

        if (Bool(a, "remove"))
        {
            log.Add(chatId, "defeat", "💀", Lang.T($"{name} выбывает из боя", $"{name} is out of the fight"), target: name);
            return;
        }

        var created = result?.Contains("добавлен противник", StringComparison.Ordinal) == true || result?.Contains("added the adversary", StringComparison.Ordinal) == true;
        if (created)
        {
            var max = S(a, "hp_max") ?? "";
            log.Add(chatId, "spawn", "⚔", Lang.T("В бой вступает: ", "Joins the fight: ") + name, target: name,
                value: max.Length > 0 ? $"{max} HP" : null);
        }

        var source = S(a, "source");
        if (TryInt(S(a, "hp_delta"), out var delta) && delta != 0)
        {
            log.Add(chatId,
                delta < 0 ? "damage" : "heal",
                delta < 0 ? "🗡" : "✚",
                "",
                target: name,
                amount: delta,
                actor: source,
                subject: name,
                value: delta < 0 ? Lang.T($"{Math.Abs(delta)} урона", $"{Math.Abs(delta)} damage") : Lang.T($"+{delta} жизни", $"+{delta} HP"));
        }

        if (TryInt(S(a, "shield"), out var shield) && shield > 0)
        {
            log.Add(chatId, "shield", "🛡", "", target: name, amount: shield,
                actor: source, subject: name, value: Lang.T("щит ", "shield ") + shield);
        }

        if (S(a, "add_effect") is { Length: > 0 } effect)
        {
            log.Add(chatId, "effect", "✦", "", target: name, actor: source, subject: name, value: Lang.T("эффект: ", "effect: ") + effect);
        }

        if (S(a, "remove_effect") is { Length: > 0 } removed)
        {
            log.Add(chatId, "cleanse", "✧", "", target: name, subject: name, value: Lang.T("снят: ", "removed: ") + removed);
        }
    }

    // ===== Герой и спутники =====

    private static void FeedHero(BattleLog log, string chatId, JsonObject a)
    {
        var source = S(a, "source");

        if (TryInt(S(a, "hp_delta"), out var delta) && delta != 0)
        {
            log.Add(chatId,
                delta < 0 ? "damage" : "heal",
                delta < 0 ? "🗡" : "✚",
                "",
                target: HeroTarget,
                amount: delta,
                actor: source,
                subject: Initiative.HeroAlias,
                value: delta < 0 ? Lang.T($"{Math.Abs(delta)} урона", $"{Math.Abs(delta)} damage") : Lang.T($"+{delta} жизни", $"+{delta} HP"));
        }

        if (TryInt(S(a, "shield"), out var shield) && shield > 0)
        {
            log.Add(chatId, "shield", "🛡", "", target: HeroTarget, amount: shield,
                actor: source, subject: Initiative.HeroAlias, value: Lang.T("щит ", "shield ") + shield);
        }

        if (S(a, "add_status") is { Length: > 0 } status)
        {
            log.Add(chatId, "effect", "✦", "", target: HeroTarget, actor: source, subject: Initiative.HeroAlias, value: Lang.T("эффект: ", "effect: ") + status);
        }

        if (S(a, "remove_status") is { Length: > 0 } removed)
        {
            log.Add(chatId, "cleanse", "✧", "", target: HeroTarget, subject: Initiative.HeroAlias, value: Lang.T("снят: ", "removed: ") + removed);
        }
    }

    private static void FeedParty(BattleLog log, string chatId, JsonObject a)
    {
        var name = S(a, "name") ?? "";
        if (name.Length == 0)
        {
            return;
        }

        if (Bool(a, "remove"))
        {
            log.Add(chatId, "defeat", "🚪", Lang.T($"{name} покидает группу", $"{name} leaves the party"), target: name);
            return;
        }

        var source = S(a, "source");
        if (TryInt(S(a, "hp_delta"), out var delta) && delta != 0)
        {
            log.Add(chatId,
                delta < 0 ? "damage" : "heal",
                delta < 0 ? "🗡" : "✚",
                "",
                target: name,
                amount: delta,
                actor: source,
                subject: name,
                value: delta < 0 ? Lang.T($"{Math.Abs(delta)} урона", $"{Math.Abs(delta)} damage") : Lang.T($"+{delta} жизни", $"+{delta} HP"));
        }

        if (TryInt(S(a, "shield"), out var shield) && shield > 0)
        {
            log.Add(chatId, "shield", "🛡", "", target: name, amount: shield,
                actor: source, subject: name, value: Lang.T("щит ", "shield ") + shield);
        }

        if (S(a, "add_status") is { Length: > 0 } status)
        {
            log.Add(chatId, "effect", "✦", "", target: name, actor: source, subject: name, value: Lang.T("эффект: ", "effect: ") + status);
        }

        if (S(a, "remove_status") is { Length: > 0 } removed)
        {
            log.Add(chatId, "cleanse", "✧", "", target: name, subject: name, value: Lang.T("снят: ", "removed: ") + removed);
        }

        if (S(a, "skill") is { Length: > 0 } skill)
        {
            log.Add(chatId, "skill", "★", $"{name}: {skill}", target: name);
        }
    }

    private static void FeedItem(BattleLog log, string chatId, JsonObject a)
    {
        var item = S(a, "item") ?? "";
        if (item.Length == 0)
        {
            return;
        }

        var owner = S(a, "owner") ?? "hero";
        var who = owner.Equals("hero", StringComparison.OrdinalIgnoreCase) ? Initiative.HeroAlias : owner;
        log.Add(chatId, "item", "🎒", $"{who}: {item}");
    }

    private static void FeedLoot(BattleLog log, string chatId, JsonObject a)
    {
        var name = S(a, "name") ?? "";
        if (name.Length == 0)
        {
            return;
        }

        var text = Lang.T("Добыча: ", "Loot: ") + name;
        if (S(a, "quantity") is { Length: > 0 } qty && qty != "1")
        {
            text += $" ×{qty}";
        }

        log.Add(chatId, "loot", "💰", text);
    }

    // ===== Разбор аргументов =====

    private static JsonObject Args(string? json)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    private static string? S(JsonObject o, string key)
    {
        try
        {
            var value = o[key]?.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static bool Bool(JsonObject o, string key)
    {
        try
        {
            return o[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryInt(string? raw, out int value) =>
        int.TryParse(raw, out value);
}
