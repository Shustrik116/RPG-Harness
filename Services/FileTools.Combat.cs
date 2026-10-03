using System.Text.Json.Nodes;

namespace RPG_Harness.Services;

/// <summary>
/// Кодовая часть порядка боя и спасбросков от смерти. Мастер решает, что делает участник,
/// а харнес хранит позицию в раунде и честно считает исход спасброска.
/// </summary>
public sealed partial class FileTools
{
    private (bool Ok, string Result) CombatTurn(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var state = _rpg.GetOrCreate(chatId);
        var action = (Str(args, "action") ?? "read").Trim().ToLowerInvariant();

        if (action is "end" or "reset")
        {
            var cleanup = state.InCombat() ? "" : CombatRules.EndCombat(state);
            state.CombatRound = 0;
            state.CombatCurrentActor = "";
            _rpg.Save(chatId, state);
            return (true, Lang.T("OK: счётчик раундов остановлен. ", "OK: the round counter is stopped. ") + cleanup);
        }

        var seats = CombatRules.ActiveSeats(state);
        if (seats.Count == 0)
            return (false, Lang.T("Ошибка: нет активной очереди боя. Сначала создай противников и брось инициативу всем участникам.", "Error: there is no active combat queue. First create adversaries and roll initiative for all participants."));

        if (action == "start")
        {
            var missing = Initiative.Missing(state);
            if (missing.Count > 0)
                return (false, Lang.T($"Ошибка: инициатива брошена не всем: {string.Join(", ", missing)}.", $"Error: initiative has not been rolled for everyone: {string.Join(", ", missing)}."));

            state.CombatRound = 1;
            state.CombatCurrentActor = seats[0];
            var startEvents = new List<CombatEvent>();
            var startNotes = CombatRules.StartTurn(state, seats[0], startEvents);
            _rpg.Save(chatId, state);
            CombatEvents.Push(chatId, startEvents);
            return (true, Lang.T($"OK: раунд 1 начат. Сейчас ходит: {state.CombatCurrentActor}. После его действия вызови combat_turn action=advance actor=\"{state.CombatCurrentActor}\".", $"OK: round 1 has started. Now acting: {state.CombatCurrentActor}. After their action call combat_turn action=advance actor=\"{state.CombatCurrentActor}\".") +
                          (startNotes.Count > 0 ? "\n" + string.Join("\n", startNotes) : ""));
        }

        if (state.CombatRound <= 0 || string.IsNullOrWhiteSpace(state.CombatCurrentActor))
            return (false, Lang.T("Ошибка: раунд ещё не начат. Вызови combat_turn action=start после бросков инициативы.", "Error: the round has not started yet. Call combat_turn action=start after the initiative rolls."));

        if (action == "advance")
        {
            var actor = (Str(args, "actor") ?? "").Trim();
            if (actor.Length == 0)
                return (false, Lang.T($"Ошибка: укажи actor — кто завершает ход. Сейчас ходит: {state.CombatCurrentActor}.", $"Error: specify actor — who finishes the turn. Now acting: {state.CombatCurrentActor}."));
            if (!CombatRules.SameActor(state, actor, state.CombatCurrentActor))
                return (false, Lang.T($"Ошибка порядка: сейчас ходит «{state.CombatCurrentActor}», а завершить ход пытается «{actor}». Не пропускай и не повторяй участников.", $"Order error: \"{state.CombatCurrentActor}\" is acting now, but \"{actor}\" tries to finish the turn. Do not skip or repeat participants."));

            // Конец хода: длительности эффектов действовавшего тикают, истёкшие спадают.
            var notes = new List<string>();
            var events = new List<CombatEvent>();
            if (CombatRules.EffectsOf(state, state.CombatCurrentActor) is { } endedEffects)
            {
                var expired = CombatEffects.Tick(endedEffects);
                if (expired.Count > 0)
                {
                    notes.Add(Lang.T($"У {state.CombatCurrentActor} спало: {string.Join(", ", expired)}.", $"Ended on {state.CombatCurrentActor}: {string.Join(", ", expired)}."));
                    events.AddRange(expired.Select(e => new CombatEvent("cleanse", "✧", null, state.CombatCurrentActor, CombatRules.LogTarget(state, state.CombatCurrentActor), null, Lang.T("спало: ", "ended: ") + e, null, null, false)));
                }
            }

            var all = Initiative.Seats(state.Initiative).Select(x => x.Name).ToList();
            var at = all.FindIndex(x => CombatRules.SameActor(state, x, state.CombatCurrentActor));
            if (at < 0) at = 0;
            string? next = null;
            var wrapped = false;
            for (var step = 1; step <= all.Count; step++)
            {
                var index = (at + step) % all.Count;
                if (index <= at) wrapped = true;
                if (CombatRules.IsActive(state, all[index]))
                {
                    next = all[index];
                    break;
                }
            }

            if (next is null || !state.InCombat())
            {
                var cleanup = CombatRules.EndCombat(state);
                _rpg.Save(chatId, state);
                CombatEvents.Push(chatId, events);
                return (true, Lang.T($"OK: противников в бою не осталось; счётчик боя остановлен. {cleanup}Подведи итог боя: award_xp encounter=true, roll_loot, offer_choices.", $"OK: no adversaries are left in the fight; the combat counter is stopped. {cleanup}Sum up the fight: award_xp encounter=true, roll_loot, offer_choices."));
            }

            if (wrapped) state.CombatRound++;
            state.CombatCurrentActor = next;

            // Начало хода: урон во времени и регенерация применяются сами, контроль — пропуск хода.
            notes.AddRange(CombatRules.StartTurn(state, next, events));
            if (CombatRules.PartyDown(state))
            {
                notes.Add(Lang.T("⚠ Вся группа выведена из строя. Не тяни бой: подведи сцену к развилке — плен, спасение за цену, чудо, бегство врагов с добычей или гибель героя.", "⚠ The whole party is down. Do not drag out the fight: bring the scene to a fork — captivity, a rescue at a price, a miracle, the enemies fleeing with loot or the hero's death."));
            }

            var over = !state.InCombat();
            var end = over ? CombatRules.EndCombat(state) : "";
            _rpg.Save(chatId, state);
            CombatEvents.Push(chatId, events);
            if (over)
            {
                return (true, Lang.T($"OK: {string.Join(" ", notes)} Противников в бою не осталось — бой окончен. {end}Вызови award_xp encounter=true, затем roll_loot; заверши ход через offer_choices.", $"OK: {string.Join(" ", notes)} No adversaries are left in the fight — the fight is over. {end}Call award_xp encounter=true, then roll_loot; finish the turn with offer_choices.").Trim());
            }

            return (true, Lang.T($"OK: {(wrapped ? $"начался раунд {state.CombatRound}" : $"раунд {state.CombatRound}")}. Сейчас ходит: {next}.", $"OK: {(wrapped ? $"round {state.CombatRound} has started" : $"round {state.CombatRound}")}. Now acting: {next}.") +
                          (notes.Count > 0 ? "\n" + string.Join("\n", notes) : ""));
        }

        if (action != "read") return (false, Lang.T("Ошибка: action должен быть start, advance, read или end.", "Error: action must be start, advance, read or end."));
        return (true, Lang.T($"Раунд {state.CombatRound}. Сейчас ходит: {state.CombatCurrentActor}. Очередь: {Initiative.Line(state.Initiative)}.", $"Round {state.CombatRound}. Now acting: {state.CombatCurrentActor}. Queue: {Initiative.Line(state.Initiative)}."));
    }

    private (bool Ok, string Result) ResolveDeathSave(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var state = _rpg.GetOrCreate(chatId);
        var target = (Str(args, "target") ?? "hero").Trim();
        var hero = target.Equals("hero", StringComparison.OrdinalIgnoreCase) ||
                   target.Equals("герой", StringComparison.OrdinalIgnoreCase) ||
                   target.Equals(state.Character.Name, StringComparison.OrdinalIgnoreCase);
        PartyMember? member = hero ? null : state.Party.FirstOrDefault(p => p.Name.Equals(target, StringComparison.OrdinalIgnoreCase));
        if (!hero && member is null) return (false, Lang.T($"Ошибка: «{target}» нет среди героя и спутников.", $"Error: \"{target}\" is neither the hero nor a companion."));

        var hp = hero ? state.Character.HpCurrent : member!.HpCurrent;
        var deathState = hero ? state.Character.DeathState : member!.DeathState;
        if (hp > 0) return (false, Lang.T("Ошибка: спасбросок от смерти нужен только при 0 HP.", "Error: a death save is needed only at 0 HP."));
        if (deathState == "dead") return (false, Lang.T("Ошибка: персонаж уже мёртв; нужен редкий эффект воскрешения, а не спасбросок.", "Error: the character is already dead; a rare resurrection effect is needed, not a saving throw."));
        if (deathState == "stable") return (false, Lang.T("Ошибка: персонаж уже стабилизирован и больше не делает спасброски от смерти.", "Error: the character is already stabilized and no longer makes death saves."));

        var roll = Random.Shared.Next(1, 21);
        if (hero)
            CombatRules.ApplyDeathRoll(state.Character, roll);
        else
            CombatRules.ApplyDeathRoll(member!, roll);

        var afterState = hero ? state.Character.DeathState : member!.DeathState;
        var successes = hero ? state.Character.DeathSaveSuccesses : member!.DeathSaveSuccesses;
        var failures = hero ? state.Character.DeathSaveFailures : member!.DeathSaveFailures;
        var afterHp = hero ? state.Character.HpCurrent : member!.HpCurrent;
        _rpg.Save(chatId, state);

        var name = hero ? (state.Character.Name.Length > 0 ? state.Character.Name : Lang.T("Герой", "Hero")) : member!.Name;
        var outcome = roll == 20 ? Lang.T("натуральная 20 — приходит в себя с 1 HP", "natural 20 — comes to with 1 HP")
            : roll == 1 ? Lang.T("натуральная 1 — два провала", "natural 1 — two failures")
            : roll >= 10 ? Lang.T("успех", "success") : Lang.T("провал", "failure");
        var final = afterHp > 0 ? Lang.T(" В сознании.", " Conscious.")
            : afterState == "stable" ? Lang.T(" Стабилизирован.", " Stabilized.")
            : afterState == "dead" ? Lang.T(" Персонаж мёртв.", " The character is dead.")
            : "";
        return (true, Lang.T($"🎲 Спасбросок от смерти — {name}: d20[{roll}] против DC 10 — {outcome}. Счёт: успехи {successes}/3, провалы {failures}/3.{final}",
                             $"🎲 Death save — {name}: d20[{roll}] vs DC 10 — {outcome}. Tally: successes {successes}/3, failures {failures}/3.{final}"));
    }
}

/// <summary>Общие инварианты боя, которыми пользуются инструменты, сводка и интерфейс.</summary>
public static class CombatRules
{
    public static bool SameActor(RpgState state, string? left, string? right)
    {
        var a = (left ?? "").Trim();
        var b = (right ?? "").Trim();
        if (a.Equals(b, StringComparison.OrdinalIgnoreCase)) return true;
        var heroName = state.Character.Name.Trim();
        return heroName.Length > 0 &&
               (a.Equals(Initiative.HeroAlias, StringComparison.OrdinalIgnoreCase) || a.Equals(heroName, StringComparison.OrdinalIgnoreCase)) &&
               (b.Equals(Initiative.HeroAlias, StringComparison.OrdinalIgnoreCase) || b.Equals(heroName, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsActive(RpgState state, string name)
    {
        if (SameActor(state, name, state.Character.Name)) return state.Character.DeathState is not ("dead" or "stable");
        var party = state.Party.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (party is not null) return party.DeathState is not ("dead" or "stable");
        var foe = state.Adversaries.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return foe?.InFight == true || foe is null; // групповая инициатива стороны остаётся активной
    }

    /// <summary>Список эффектов участника по имени из очереди (герой, спутник, противник); null — не найден.</summary>
    public static List<string>? EffectsOf(RpgState state, string? name)
    {
        var who = (name ?? "").Trim();
        if (who.Length == 0) return null;
        if (SameActor(state, who, state.Character.Name) || who.Equals(Initiative.HeroAlias, StringComparison.OrdinalIgnoreCase)) return state.Character.Status;
        return state.Party.FirstOrDefault(p => p.Name.Equals(who, StringComparison.OrdinalIgnoreCase))?.Status
               ?? state.Adversaries.FirstOrDefault(a => a.Name.Equals(who, StringComparison.OrdinalIgnoreCase))?.Effects;
    }

    /// <summary>Метка карточки для анимации журнала: «@hero» у героя, иначе имя.</summary>
    public static string LogTarget(RpgState state, string name) =>
        SameActor(state, name, state.Character.Name) || name.Equals(Initiative.HeroAlias, StringComparison.OrdinalIgnoreCase) ? BattleLogFeed.HeroTarget : name;

    /// <summary>Герой и все спутники выведены из строя: 0 ХП (при смерти, стабилизированы) или мертвы.</summary>
    public static bool PartyDown(RpgState state) =>
        state.Character.HpCurrent <= 0 && state.Party.All(p => p.HpCurrent <= 0);

    /// <summary>
    /// Начало хода участника: урон во времени и регенерация применяются кодом, контроль означает пропуск хода,
    /// «при смерти» — спасбросок. Возвращает заметки для мастера; события журнала добавляет в events.
    /// </summary>
    public static List<string> StartTurn(RpgState state, string name, List<CombatEvent> events)
    {
        var notes = new List<string>();
        var effects = EffectsOf(state, name);
        if (effects is null) return notes;

        var isHero = SameActor(state, name, state.Character.Name) || name.Equals(Initiative.HeroAlias, StringComparison.OrdinalIgnoreCase);
        var member = isHero ? null : state.Party.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var foe = isHero || member is not null ? null : state.Adversaries.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var target = LogTarget(state, name);

        foreach (var effect in effects.ToList())
        {
            if (CombatEffects.Periodic(effect) is not { } tick) continue;
            var (count, sides) = (int.Parse(tick.Dice.Split('d')[0]), int.Parse(tick.Dice.Split('d')[1]));
            var amount = Enumerable.Range(0, count).Sum(_ => Random.Shared.Next(1, sides + 1));
            var title = CombatEffects.BaseName(effect);
            if (tick.Heal)
            {
                // Регенерация не действует на нежить, конструктов и машины.
                if (foe is not null && foe.Kind is "undead" or "construct" or "mech" or "drone" or "program" or "vehicle") continue;
                int before, after, max;
                if (isHero) { before = state.Character.HpCurrent; max = state.Character.HpMax; state.Character.HpCurrent = after = Math.Min(max, before + amount); if (after > 0) Revive(state.Character); }
                else if (member is not null) { before = member.HpCurrent; max = member.HpMax; member.HpCurrent = after = Math.Min(max, before + amount); if (after > 0) Revive(member); }
                else if (foe is not null && foe.HpCurrent > 0) { before = foe.HpCurrent; max = foe.HpMax; foe.HpCurrent = after = Math.Min(max, before + amount); }
                else continue;
                notes.Add(Lang.T($"{title}: {name} +{after - before} ХП ({before}→{after}/{max}) — уже применено.", $"{title}: {name} +{after - before} HP ({before}→{after}/{max}) — already applied."));
                events.Add(new CombatEvent("heal", "✚", title, name, target, after - before, Lang.T($"+{after - before} жизни", $"+{after - before} HP"), $"{tick.Dice} = {amount}", null, false));
                continue;
            }

            string result;
            if (isHero || member is not null)
            {
                var hp = isHero ? state.Character.HpCurrent : member!.HpCurrent;
                var shield = isHero ? state.Character.Shield : member!.Shield;
                var absorbed = Math.Min(shield, amount);
                var rest = amount - absorbed;
                if (isHero) state.Character.Shield -= absorbed; else member!.Shield -= absorbed;
                if (rest > 0 && hp == 0)
                {
                    if (isHero) DamageWhileDying(state.Character); else DamageWhileDying(member!);
                    result = Lang.T("по лежащему — +1 провал спасброска", "on the downed — +1 death save failure");
                }
                else
                {
                    var after = Math.Max(0, hp - rest);
                    if (isHero) { state.Character.HpCurrent = after; if (after == 0) EnterDying(state.Character); }
                    else { member!.HpCurrent = after; if (after == 0) EnterDying(member); }
                    result = $"{Lang.T("ХП", "HP")} {hp}→{after}" + (after == 0 ? Lang.T(" — падает «при смерти»", " — falls \"dying\"") : "");
                }
            }
            else if (foe is not null && foe.HpCurrent > 0)
            {
                var absorbed = Math.Min(foe.Shield, amount);
                foe.Shield -= absorbed;
                var before = foe.HpCurrent;
                foe.HpCurrent = Math.Max(0, before - (amount - absorbed));
                result = $"{Lang.T("ХП", "HP")} {before}→{foe.HpCurrent}" + (foe.HpCurrent == 0 ? Lang.T(" — повержен", " — defeated") : "");
            }
            else
            {
                continue;
            }

            notes.Add(Lang.T($"{title}: {name} получает {amount} урона {tick.Type} ({tick.Dice}), {result} — уже применено, не дублируй.", $"{title}: {name} takes {amount} {tick.Type} damage ({tick.Dice}), {result} — already applied, do not duplicate."));
            events.Add(new CombatEvent("damage", "🗡", title, name, target, -amount, Lang.T($"{amount} урона", $"{amount} damage"), $"{tick.Dice} = {amount}", null, false));
        }

        if (foe is not null && foe.HpCurrent <= 0)
        {
            notes.Add(Lang.T($"{name} пал от эффектов в начале хода — сразу вызови combat_turn advance actor=\"{name}\".", $"{name} fell from effects at the start of the turn — immediately call combat_turn advance actor=\"{name}\"."));
            notes.AddRange(CheckMorale(state, events));
            return notes;
        }

        var hpNow = isHero ? state.Character.HpCurrent : member?.HpCurrent ?? foe?.HpCurrent ?? 0;
        if ((isHero || member is not null) && hpNow <= 0)
        {
            var death = isHero ? state.Character.DeathState : member!.DeathState;
            notes.Add(death == "dying"
                ? Lang.T($"{name} «при смерти»: в начале хода — resolve_death_save, затем advance.", $"{name} is \"dying\": at the start of the turn — resolve_death_save, then advance.")
                : Lang.T($"{name} без сознания ({(death == "stable" ? "стабилизирован" : death)}) — ход пропускается, вызови advance.", $"{name} is unconscious ({(death == "stable" ? "stabilized" : death)}) — the turn is skipped, call advance."));
            return notes;
        }

        var control = effects.Where(CombatEffects.SkipsTurn).Select(CombatEffects.BaseName).ToList();
        if (control.Count > 0)
        {
            notes.Add(Lang.T($"{name} не может действовать ({string.Join(", ", control)}): опиши пропуск хода (при положенном — повторный спасбросок от контроля) и вызови advance actor=\"{name}\".", $"{name} cannot act ({string.Join(", ", control)}): describe the skipped turn (where due — a repeated saving throw against the control) and call advance actor=\"{name}\"."));
        }
        else if (effects.Count > 0)
        {
            notes.Add(Lang.T($"На {name}: {string.Join(", ", effects)}.", $"On {name}: {string.Join(", ", effects)}."));
        }

        return notes;
    }

    /// <summary>
    /// Бой окончен: с героя и спутников снимаются эффекты на раунды и «до конца боя», щиты обнуляются,
    /// счётчик раундов останавливается. Возвращает строку для мастера (пусто — снимать нечего).
    /// </summary>
    public static string EndCombat(RpgState state)
    {
        state.CombatRound = 0;
        state.CombatCurrentActor = "";
        var gone = new List<string>();
        var heroName = state.Character.Name.Length > 0 ? state.Character.Name : Initiative.HeroAlias;
        void Clear(string who, List<string> status, Func<int> shield, Action zero)
        {
            var cleared = CombatEffects.ClearAfterCombat(status);
            if (shield() > 0) { cleared.Add(Lang.T("щит", "shield")); zero(); }
            if (cleared.Count > 0) gone.Add($"{who}: {string.Join(", ", cleared)}");
        }

        Clear(heroName, state.Character.Status, () => state.Character.Shield, () => state.Character.Shield = 0);
        foreach (var m in state.Party)
        {
            var member = m;
            Clear(member.Name, member.Status, () => member.Shield, () => member.Shield = 0);
        }

        return gone.Count > 0 ? Lang.T($"Боевые эффекты и щиты сняты ({string.Join("; ", gone)}). ", $"Combat effects and shields removed ({string.Join("; ", gone)}). ") : "";
    }

    /// <summary>Бонус спасброска противника: мастерство уровня + типичная характеристика, роль и архетип.</summary>
    public static int FoeSaveBonus(Adversary foe, DndStat stat)
    {
        var level = Math.Max(1, Progression.ParseLevel(foe.Level));
        var baseBonus = Progression.Proficiency(level) + (level < 4 ? 1 : level < 8 ? 2 : 3);
        var role = foe.ThreatTier switch { "minion" => -2, "elite" => 1, "quest_boss" => 2, "dungeon_boss" or "arc_boss" => 3, _ => 0 };
        var mental = stat is DndStat.Int or DndStat.Wis or DndStat.Cha;
        var arch = foe.Archetype switch
        {
            "caster" => mental ? 2 : -1,
            "brute" => mental ? -2 : stat is DndStat.Str or DndStat.Con ? 2 : 0,
            "skirmisher" or "sniper" => stat == DndStat.Dex ? 2 : 0,
            "defender" => stat is DndStat.Con or DndStat.Str ? 1 : 0,
            _ => 0,
        };
        var beast = foe.Kind is "beast" && mental ? -2 : 0;
        return baseBonus + role + arch + beast;
    }

    /// <summary>Бонус инициативы противника: ловкачи и стрелки быстрее, громилы и защитники медленнее, боссы — опытнее.</summary>
    public static int FoeInitiativeBonus(Adversary foe)
    {
        var level = Math.Max(1, Progression.ParseLevel(foe.Level));
        var arch = foe.Archetype switch { "skirmisher" => 3, "sniper" => 2, "brute" or "defender" => 0, _ => 1 };
        var role = foe.ThreatTier switch { "elite" => 1, "quest_boss" or "dungeon_boss" or "arc_boss" => 2, "minion" => -1, _ => 0 };
        return arch + role + level / 6;
    }

    /// <summary>Типы существ без страха: нежить, конструкты, слизи, растения, элементали и машины не бегут.</summary>
    private static bool Fearless(Adversary a) =>
        a.Kind is "undead" or "construct" or "ooze" or "plant" or "elemental" or "mech" or "drone" or "program" or "vehicle" ||
        a.ThreatTier is "dungeon_boss" or "arc_boss";

    private static bool Beastly(Adversary a) => a.Kind is "beast" or "monstrosity" or "mutant" or "dragon" or "demon" or "aberration";

    /// <summary>
    /// Проверки морали после урона: тяжело раненый (≤⅓ ХП), павший вожак (элита или босс), полегла половина отряда (от трёх врагов).
    /// Каждая причина проверяется один раз. Провал — противник бежит или сдаётся (звери и чудовища только бегут).
    /// </summary>
    public static List<string> CheckMorale(RpgState state, List<CombatEvent>? events = null)
    {
        var notes = new List<string>();
        var foes = state.Adversaries;
        if (foes.Count == 0) return notes;
        var leaderFell = foes.Any(a => a.ThreatTier is "elite" or "quest_boss" or "dungeon_boss" or "arc_boss" && !a.InFight);
        var leaderStands = foes.Any(a => a.ThreatTier is "elite" or "quest_boss" or "dungeon_boss" or "arc_boss" && a.InFight);
        var routed = foes.Count >= 3 && foes.Count(a => !a.InFight) * 2 >= foes.Count;

        foreach (var foe in foes.Where(a => a.InFight && !Fearless(a)).ToList())
        {
            string? trigger = null;
            var dc = 10;
            if (foe.HpMax > 0 && foe.HpCurrent * 3 <= foe.HpMax && !foe.MoraleTests.Contains("wounded"))
            {
                trigger = "wounded";
            }
            else if (leaderFell && foe.ThreatTier is "minion" or "ordinary" && !foe.MoraleTests.Contains("leader"))
            {
                trigger = "leader";
                dc = 12;
            }
            else if (routed && foe.ThreatTier is "minion" or "ordinary" or "elite" && !foe.MoraleTests.Contains("rout"))
            {
                trigger = "rout";
            }

            if (trigger is null) continue;
            if (foe.ThreatTier == "quest_boss" && trigger != "wounded") continue;
            foe.MoraleTests.Add(trigger);

            var bonus = foe.ThreatTier switch { "minion" => -2, "elite" => 2, "quest_boss" => 4, _ => 0 } +
                        (foe.Kind == "beast" ? -1 : 0) + (leaderStands && foe.ThreatTier is "minion" or "ordinary" ? 2 : 0) +
                        (foe.Effects.Any(e => System.Text.RegularExpressions.Regex.IsMatch(e, "страх|испуг|паник|fear|fright|panic", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) ? -3 : 0);
            var face = Random.Shared.Next(1, 21);
            var total = face + bonus;
            var why = trigger switch { "wounded" => Lang.T($"тяжело ранен, ХП {foe.HpCurrent}/{foe.HpMax}", $"badly wounded, HP {foe.HpCurrent}/{foe.HpMax}"), "leader" => Lang.T("пал вожак", "the leader fell"), _ => Lang.T("отряд полёг наполовину", "half the squad fell") };
            if (total >= dc)
            {
                notes.Add($"{Lang.T("Мораль", "Morale")}: {foe.Name} ({why}) — d20 {face}{(bonus != 0 ? $" {(bonus > 0 ? "+" : "−")} {Math.Abs(bonus)}" : "")} = {total} {Lang.T("против", "vs")} {dc}: {Lang.T("держится", "holds")}.");
                continue;
            }

            var surrender = !Beastly(foe) && Random.Shared.Next(1, 7) >= 4;
            foe.Morale = surrender ? "surrendered" : "fled";
            notes.Add($"{Lang.T("Мораль", "Morale")}: {foe.Name} ({why}) — d20 {face}{(bonus != 0 ? $" {(bonus > 0 ? "+" : "−")} {Math.Abs(bonus)}" : "")} = {total} {Lang.T("против", "vs")} {dc}: " +
                      (surrender
                          ? Lang.T("ДРОГНУЛ И СДАЁТСЯ — бросает оружие, просит пощады. Он вне боя; герой решает его судьбу (допрос, плен, отпустить).", "BROKE AND SURRENDERS — drops the weapon, begs for mercy. Out of the fight; the hero decides their fate (interrogation, captivity, release).")
                          : Lang.T("ДРОГНУЛ И БЕЖИТ — выходит из боя и скрывается; может вернуться с подмогой или стать зацепкой.", "BROKE AND FLEES — leaves the fight and hides; may return with help or become a lead.")));
            events?.Add(new CombatEvent("defeat", surrender ? "🏳" : "🏃", null, null, foe.Name, null, null, null, null, false,
                Text: surrender ? Lang.T($"{foe.Name} сдаётся", $"{foe.Name} surrenders") : Lang.T($"{foe.Name} бежит с поля боя", $"{foe.Name} flees the battlefield")));
        }

        return notes;
    }

    public static List<string> ActiveSeats(RpgState state) => Initiative.Seats(state.Initiative)
        .Select(x => x.Name).Where(x => IsActive(state, x)).ToList();

    public static void EnterDying(CharacterSheet c)
    {
        if (c.HpCurrent > 0 || c.DeathState == "dead") return;
        if (c.DeathState is not ("dying" or "stable"))
        {
            c.DeathSaveSuccesses = 0;
            c.DeathSaveFailures = 0;
        }
        c.DeathState = "dying";
    }

    public static void EnterDying(PartyMember c)
    {
        if (c.HpCurrent > 0 || c.DeathState == "dead") return;
        if (c.DeathState is not ("dying" or "stable"))
        {
            c.DeathSaveSuccesses = 0;
            c.DeathSaveFailures = 0;
        }
        c.DeathState = "dying";
    }

    public static void Revive(CharacterSheet c)
    {
        if (c.HpCurrent <= 0) return;
        c.DeathState = "";
        c.DeathSaveSuccesses = c.DeathSaveFailures = 0;
    }

    public static void Revive(PartyMember c)
    {
        if (c.HpCurrent <= 0) return;
        c.DeathState = "";
        c.DeathSaveSuccesses = c.DeathSaveFailures = 0;
    }

    public static void DamageWhileDying(CharacterSheet c)
    {
        if (c.HpCurrent == 0 && c.DeathState != "dead")
        {
            if (c.DeathState == "stable")
            {
                c.DeathState = "dying";
                c.DeathSaveSuccesses = 0;
                c.DeathSaveFailures = 0;
            }
            EnterDying(c);
            c.DeathSaveFailures = Math.Min(3, c.DeathSaveFailures + 1);
            if (c.DeathSaveFailures >= 3) c.DeathState = "dead";
        }
    }

    public static void DamageWhileDying(PartyMember c)
    {
        if (c.HpCurrent == 0 && c.DeathState != "dead")
        {
            if (c.DeathState == "stable")
            {
                c.DeathState = "dying";
                c.DeathSaveSuccesses = 0;
                c.DeathSaveFailures = 0;
            }
            EnterDying(c);
            c.DeathSaveFailures = Math.Min(3, c.DeathSaveFailures + 1);
            if (c.DeathSaveFailures >= 3) c.DeathState = "dead";
        }
    }

    public static void ApplyDeathRoll(CharacterSheet c, int roll)
    {
        EnterDying(c);
        if (roll == 20) { c.HpCurrent = 1; Revive(c); return; }
        if (roll == 1) c.DeathSaveFailures = Math.Min(3, c.DeathSaveFailures + 2);
        else if (roll >= 10) c.DeathSaveSuccesses = Math.Min(3, c.DeathSaveSuccesses + 1);
        else c.DeathSaveFailures = Math.Min(3, c.DeathSaveFailures + 1);
        if (c.DeathSaveFailures >= 3) c.DeathState = "dead";
        else if (c.DeathSaveSuccesses >= 3) c.DeathState = "stable";
    }

    public static void ApplyDeathRoll(PartyMember c, int roll)
    {
        EnterDying(c);
        if (roll == 20) { c.HpCurrent = 1; Revive(c); return; }
        if (roll == 1) c.DeathSaveFailures = Math.Min(3, c.DeathSaveFailures + 2);
        else if (roll >= 10) c.DeathSaveSuccesses = Math.Min(3, c.DeathSaveSuccesses + 1);
        else c.DeathSaveFailures = Math.Min(3, c.DeathSaveFailures + 1);
        if (c.DeathSaveFailures >= 3) c.DeathState = "dead";
        else if (c.DeathSaveSuccesses >= 3) c.DeathState = "stable";
    }
}
