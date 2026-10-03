using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RPG_Harness.Services;

/// <summary>
/// Тактический слой: атака кодом (resolve_attack), проверка навыка со степенями успеха (skill_check), отдых,
/// время и часы угрозы, отношение фракций. Модель решает, кто бьёт кого и чем, что пытается сделать герой
/// и чего хочет злодей; харнес бросает кости, берёт числа из листов и честно применяет результат.
/// </summary>
public sealed partial class FileTools
{
    private static readonly string[] TacticsTools = { "resolve_attack", "skill_check", "rest", "update_clock", "update_faction" };

    private IEnumerable<JsonObject> TacticsDefinitions()
    {
        yield return Def("resolve_attack",
            L("Честно разыграть действие в бою ОДНИМ вызовом: харнес сам берёт бонус атаки, КБ, урон и DC из листов и статблоков, бросает d20 и урон, " +
            "учитывает криты, щиты, преимущество от эффектов (ослеплён, опутан, невидим…), сопротивления и «слом» боссов, сразу списывает ХП, " +
            "проверяет мораль врагов и пишет журнал боя. Это основной способ атаки, навыка и лечения в бою — не дублируй результат через roll_dice и hp_delta. " +
            "mode: attack — бросок атаки против КБ (по умолчанию); save — цели делают спасбросок против DC атакующего (область, яд, контроль); " +
            "heal — лечение; shield — энергетический щит; buff — только эффект без броска. Урон по умолчанию — оружие или статблок атакующего; " +
            "для навыка передай damage в костях мощи («1.5К огнём») — харнес переведёт в кости уровня владельца и прибавит модификатор. " +
            "Противник с несколькими атаками делает их все одним вызовом (attacks=1 — только одну).",
            "Fairly resolve a combat action in ONE call: the harness takes the attack bonus, AC, damage and DC from the sheets and stat blocks itself, rolls the d20 and the damage, " +
            "accounts for crits, shields, advantage from effects (blinded, entangled, invisible…), resistances and boss \"breaks\", deducts HP immediately, " +
            "checks enemy morale and writes the battle log. This is the main way to attack, use a skill or heal in combat — do not duplicate the result via roll_dice and hp_delta. " +
            "mode: attack — an attack roll against AC (default); save — the targets make a saving throw against the attacker's DC (an area, poison, control); " +
            "heal — healing; shield — an energy shield; buff — only an effect without a roll. Damage by default — the attacker's weapon or stat block; " +
            "for a skill pass damage in power dice (\"1.5K fire\") — the harness converts it into the owner's level dice and adds the modifier. " +
            "An adversary with several attacks makes them all in one call (attacks=1 — only one)."),
            ("attacker", "string", L("Кто действует: hero / имя героя, имя спутника или противника", "Who acts: hero / the hero's name, a companion's or an adversary's name"), true),
            ("target", "string", L("Цель или несколько целей через запятую (область): имена противников, hero, спутники", "A target or several comma-separated targets (an area): adversary names, hero, companions"), true),
            ("mode", "string", L("attack / save / heal / shield / buff (по умолчанию attack)", "attack / save / heal / shield / buff (default attack)"), false),
            ("skill", "string", L("Название навыка или приёма — для журнала и описания (опционально)", "The name of the skill or move — for the log and the description (optional)"), false),
            ("weapon", "string", L("Для героя и спутника: weapon (оружие, по умолчанию) или spell (заклинание ключевой характеристикой)", "For the hero and a companion: weapon (default) or spell (a spell with the key ability)"), false),
            ("damage", "string", L("Свой урон/лечение/щит: «1.5К огнём», «2К», «3d6 огонь», «2d8+3». Не указан — урон оружия или статблока", "Custom damage/healing/shield: \"1.5K fire\", \"2K\", \"3d6 fire\", \"2d8+3\". If not given — weapon or stat block damage"), false),
            ("damage_type", "string", L("Тип урона, если не указан в damage: огонь, рубящий, яд…", "The damage type if not given in damage: fire, slashing, poison…"), false),
            ("save", "string", L("Для mode=save: характеристика спасброска str / dex / con / int / wis / cha (по умолчанию dex)", "For mode=save: the saving throw ability str / dex / con / int / wis / cha (default dex)"), false),
            ("dc", "integer", L("Для mode=save: своя Сложность; по умолчанию DC навыков атакующего", "For mode=save: a custom DC; default — the attacker's skill DC"), false),
            ("half_on_save", "boolean", L("Для mode=save: при успешном спасброске половина урона (по умолчанию true)", "For mode=save: half damage on a successful save (default true)"), false),
            ("advantage", "string", L("adv / dis — преимущество или помеха по обстановке (засада, высота, укрытие); эффекты учитываются сами", "adv / dis — advantage or disadvantage from the situation (an ambush, high ground, cover); effects are accounted for automatically"), false),
            ("modifier", "integer", L("Ситуативная поправка к броску атаки (опционально)", "A situational modifier to the attack roll (optional)"), false),
            ("cover", "integer", L("Укрытие цели: +2 КБ (половинное) или +5 (хорошее)", "The target's cover: +2 AC (half) or +5 (good)"), false),
            ("resist", "string", L("Как цель переносит этот тип урона (из Bestiary.md): weak / resist / immune. weak — уязвимость: ×1.5 и снимает сегмент стойкости", "How the target takes this damage type (from Bestiary.md): weak / resist / immune. weak — vulnerability: ×1.5 and removes a poise segment"), false),
            ("attacks", "integer", L("Сколько атак сделать (1–4). По умолчанию 1, у противника — число атак статблока", "How many attacks to make (1–4). Default 1, for an adversary — the stat block's number of attacks"), false),
            ("mana", "integer", L("Цена навыка в мане — спишется с героя или спутника; если маны не хватает, действие не состоится", "The skill's mana cost — deducted from the hero or the companion; if there is not enough mana, the action does not happen"), false),
            ("effect", "string", L("Эффект на цель при попадании / проваленном спасброске (для heal, shield, buff — всегда): «поджог», «оглушение», «благословение»", "An effect on the target on a hit / a failed save (for heal, shield, buff — always): \"burning\", \"stun\", \"blessing\""), false),
            ("effect_rounds", "integer", L("Длительность эффекта в раундах (по умолчанию типичная для эффекта)", "The effect's duration in rounds (default — typical for the effect)"), false));

        yield return Def("skill_check",
            L("Проверка характеристики или навыка героя либо спутника: харнес сам прибавляет модификатор с вещами, мастерство, учитывает эффекты " +
            "(отравление, проклятие — помеха) и называет СТЕПЕНЬ успеха: блестящий успех (≥ DC+10 или натуральная 20 — дополнительная выгода), успех, " +
            "успех ценой (не хватило 1–3: цель достигнута, но с осложнением — шум, потеря времени, вещи, положения; игрок может отказаться от цены), " +
            "провал (натуральная 1 — ещё и с осложнением). Если игрок бросил d20 кнопкой в чате — передай выпавшее число в roll; иначе харнес бросит сам " +
            "(спутник, пассивная проверка). Не считай модификаторы сам и не пересказывай игроку формулу — он видит её в результате.",
            "An ability or skill check of the hero or a companion: the harness adds the modifier with items and proficiency itself, accounts for effects " +
            "(poison, curse — disadvantage) and names the DEGREE of success: brilliant success (≥ DC+10 or a natural 20 — an extra benefit), success, " +
            "success at a cost (short by 1–3: the goal is achieved, but with a complication — noise, lost time, items, position; the player may refuse the cost), " +
            "failure (a natural 1 — also with a complication). If the player rolled a d20 with the button in the chat, pass the rolled number in roll; otherwise the harness rolls itself " +
            "(a companion, a passive check). Do not calculate modifiers yourself and do not retell the formula to the player — they see it in the result."),
            ("who", "string", L("hero (по умолчанию) или имя спутника", "hero (default) or a companion's name"), false),
            ("skill", "string", L("Навык (Атлетика, Скрытность, Внимательность, Убеждение…) или характеристика (str, dex, con, int, wis, cha)", "A skill (Athletics, Stealth, Perception, Persuasion…) or an ability (str, dex, con, int, wis, cha)"), true),
            ("dc", "integer", L("Сложность: 5 очень легко, 10 легко, 15 средне, 20 трудно, 25 почти невозможно", "DC: 5 very easy, 10 easy, 15 medium, 20 hard, 25 nearly impossible"), true),
            ("roll", "integer", L("Выпавшее на d20 игрока из сообщения «🎲 … d20: N» (1–20). Не указан — харнес бросит сам", "The player's d20 result from the message \"🎲 … d20: N\" (1–20). If not given, the harness rolls itself"), false),
            ("proficient", "boolean", L("Навык класса или прошлого героя — прибавить бонус мастерства", "A class or background skill of the hero — add the proficiency bonus"), false),
            ("advantage", "string", L("adv / dis — по обстановке (помощь, инструменты, свойство вещи, спешка)", "adv / dis — from the situation (help, tools, an item property, haste)"), false),
            ("bonus", "integer", L("Ситуативная поправка (опционально)", "A situational modifier (optional)"), false),
            ("save", "boolean", L("Это спасбросок (вне боя: ловушка, яд, страх), а не проверка навыка", "This is a saving throw (outside combat: a trap, poison, fear), not a skill check"), false),
            ("reason", "string", L("Что пытается сделать: «перелезть стену», «уговорить стражника»", "What they try to do: \"climb the wall\", \"persuade the guard\""), false));

        yield return Def("rest",
            L("Отдых героя и группы — восстанавливает ХП и ману по правилам и двигает время; вне боя. " +
            "short — короткий привал (около часа): +четверть ХП и маны, стабилизированные приходят в себя; не больше двух между долгими. " +
            "long — ночлег (8 часов): полное восстановление, снимаются эффекты «до отдыха», сбрасываются свойства «раз в день», наступает утро следующего дня, тикают часы угрозы. " +
            "В диком месте харнес бросает ночную тревогу (wild ~25%, hostile ~50%): тогда отдых прерван — разыграй тревогу и после неё вызови rest снова с resume=true. " +
            "Ночлег вне поселения съедает один паёк из сумки; без еды восстанавливается только половина. Не восстанавливай ХП и ману отдыхом вручную.",
            "Rest for the hero and the party — restores HP and mana by the rules and moves time; outside combat. " +
            "short — a short halt (about an hour): +a quarter of HP and mana, the stabilized come to; no more than two between long rests. " +
            "long — a night's rest (8 hours): full recovery, \"until rest\" effects are removed, \"once a day\" properties reset, the morning of the next day comes, threat clocks tick. " +
            "In a wild place the harness rolls a night alarm (wild ~25%, hostile ~50%): then the rest is interrupted — play out the alarm and call rest again with resume=true afterwards. " +
            "A night's rest outside a settlement eats one ration from the bag; without food only half is restored. Do not restore HP and mana by resting manually."),
            ("kind", "string", "short / long", true),
            ("place", "string", L("safe — трактир, дом, город; wild — лагерь в дикой местности; hostile — враждебная территория, данж", "safe — an inn, a house, a town; wild — a camp in the wilderness; hostile — hostile territory, a dungeon"), false),
            ("resume", "boolean", L("Продолжить отдых после разыгранной тревоги — без нового броска", "Continue the rest after a played-out alarm — without a new roll"), false),
            ("note", "string", L("Где и как отдыхают — для журнала (опционально)", "Where and how they rest — for the journal (optional)"), false));

        yield return Def("update_clock",
            L("Часы угрозы и прогресса — мир живёт без героя. Часы из 4/6/8 сегментов заполняются тиками: событиями сцены (шум, провал, промедление, " +
            "успех соперника) и ходом дней (per_days — харнес тикает сам при отдыхе и дороге). Заполнились — наступает объявленное событие (on_fill), " +
            "харнес сообщит об этом. Угрозы (замысел злодея, эпидемия, облава) делай тайными (secret=true), прогресс героя (взлом, расследование, переговоры) — открытым.",
            "Threat and progress clocks — the world lives without the hero. Clocks of 4/6/8 segments fill with ticks: scene events (noise, a failure, a delay, " +
            "a rival's success) and the passing of days (per_days — the harness ticks itself on rest and travel). When filled, the announced event happens (on_fill), " +
            "the harness reports it. Make threats (a villain's plot, an epidemic, a raid) secret (secret=true), the hero's progress (a heist, an investigation, negotiations) open."),
            ("name", "string", L("Название: «Ритуал в катакомбах», «Облава стражи», «Подкоп под стену»", "Name: \"Ritual in the Catacombs\", \"Guard Raid\", \"Tunnel under the Wall\""), true),
            ("segments", "integer", L("4 — скоро, 6 — обычно, 8 — долго (при создании)", "4 — soon, 6 — usual, 8 — long (on creation)"), false),
            ("tick", "integer", L("Сколько сегментов заполнить (минус — откатить): обычно 1, серьёзный провал или промедление — 2", "How many segments to fill (minus — roll back): usually 1, a serious failure or delay — 2"), false),
            ("kind", "string", L("threat — угроза, progress — дело героя, rival — соперник (при создании)", "threat — a threat, progress — the hero's undertaking, rival — a rival (on creation)"), false),
            ("secret", "boolean", L("Тайные часы: видит только мастер", "Secret clocks: only the master sees them"), false),
            ("per_days", "integer", L("Тикать самим раз в N дней (0 — только по событиям)", "Tick on their own once every N days (0 — only by events)"), false),
            ("on_fill", "string", L("Что случится, когда часы заполнятся", "What happens when the clock fills"), false),
            ("reset", "boolean", L("Обнулить заполнение (угрозу отбили, дело начато заново)", "Reset the fill (the threat was repelled, the undertaking started over)"), false),
            ("remove", "boolean", L("Убрать часы (угроза устранена, цель достигнута)", "Remove the clock (the threat is eliminated, the goal achieved)"), false));

        yield return Def("update_faction",
            L("Отношение фракции к герою: число от −100 (кровные враги) до +100 (союзники). Меняй после поступков, которые фракция заметила: " +
            "помощь и услуги +5…+20, оскорбление, кража, убийство своих −10…−40. Харнес называет итоговое отношение (враги / враждебны / нейтральны / " +
            "дружелюбны / союзники) — отыгрывай его: цены, доступ, засады, помощь. Фракции в Factions.md — их история; здесь — число.",
            "A faction's attitude to the hero: a number from −100 (blood enemies) to +100 (allies). Change it after deeds the faction noticed: " +
            "help and services +5…+20, an insult, theft, killing their own −10…−40. The harness names the resulting attitude (enemies / hostile / neutral / " +
            "friendly / allies) — play it out: prices, access, ambushes, help. Factions in Factions.md are their story; here is the number."),
            ("name", "string", L("Название фракции", "The faction name"), true),
            ("delta", "integer", L("Изменение отношения (−40…+40)", "The attitude change (−40…+40)"), false),
            ("set", "integer", L("Задать отношение напрямую (при знакомстве с фракцией: −100…100)", "Set the attitude directly (on first contact with the faction: −100…100)"), false),
            ("reason", "string", L("За что — фракция это запомнит", "What for — the faction will remember it"), false),
            ("note", "string", L("Кто это и что фракция думает о герое (опционально)", "Who they are and what the faction thinks of the hero (optional)"), false),
            ("remove", "boolean", L("Убрать фракцию из списка (распущена, уничтожена)", "Remove the faction from the list (disbanded, destroyed)"), false));
    }

    // ===== Бойцы: герой, спутник, противник =====

    /// <summary>Участник действия — одна из трёх сторон книги героя.</summary>
    private sealed class Actor
    {
        public CharacterSheet? Hero;
        public PartyMember? Member;
        public Adversary? Foe;
        public string Name = "";

        public bool IsPc => Hero is not null || Member is not null;
        public ICombatant? Pc => (ICombatant?)Hero ?? Member;
        public List<string> Effects => Hero?.Status ?? Member?.Status ?? Foe!.Effects;
        public string LogTarget => Hero is not null ? BattleLogFeed.HeroTarget : Name;
        public int Hp => Hero?.HpCurrent ?? Member?.HpCurrent ?? Foe!.HpCurrent;
        public int HpMax => Hero?.HpMax ?? Member?.HpMax ?? Foe!.HpMax;
        public int Shield => Hero?.Shield ?? Member?.Shield ?? Foe!.Shield;
        public int Level => Progression.ParseLevel(Hero?.Level ?? Member?.Level ?? Foe!.Level);
        public bool Down => Foe is not null ? !Foe.InFight : Hp <= 0;
    }

    private static bool IsHeroName(RpgState state, string name) =>
        name.Equals("hero", StringComparison.OrdinalIgnoreCase) || name.Equals("герой", StringComparison.OrdinalIgnoreCase) || name.Equals("the hero", StringComparison.OrdinalIgnoreCase) ||
        name.Equals(Initiative.HeroAlias, StringComparison.OrdinalIgnoreCase) ||
        (state.Character.Name.Trim().Length > 0 && name.Equals(state.Character.Name.Trim(), StringComparison.OrdinalIgnoreCase));

    private static Actor? FindActor(RpgState state, string? raw)
    {
        var name = (raw ?? "").Trim();
        if (name.Length == 0) return null;
        if (IsHeroName(state, name))
            return new Actor { Hero = state.Character, Name = state.Character.Name.Trim().Length > 0 ? state.Character.Name.Trim() : Initiative.HeroAlias };

        var member = state.Party.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (member is not null) return new Actor { Member = member, Name = member.Name };
        var foe = state.Adversaries.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (foe is not null) return new Actor { Foe = foe, Name = foe.Name };

        // Неточное имя («гоблин 2» без дефиса, «Лира» вместо «Лира Тенистая») — только если совпадение единственное.
        var fuzzy = state.Party.Where(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase)).Select(p => new Actor { Member = p, Name = p.Name })
            .Concat(state.Adversaries.Where(a => a.Name.Contains(name, StringComparison.OrdinalIgnoreCase) || name.Contains(a.Name, StringComparison.OrdinalIgnoreCase))
                .Select(a => new Actor { Foe = a, Name = a.Name }))
            .ToList();
        return fuzzy.Count == 1 ? fuzzy[0] : null;
    }

    /// <summary>Класс брони с эффектами (защита, замедление…) — то, против чего бросают атаку.</summary>
    private static int EffectiveAc(Actor a) =>
        (a.Pc is { } pc ? ItemStats.ArmorClass(pc) : a.Foe!.Ac) + CombatEffects.Read(a.Effects).Ac;

    /// <summary>Модификатор спасброска: у героя и спутника — характеристика с вещами (+ мастерство по двум главным характеристикам архетипа),
    /// у противника — по уровню, роли и архетипу.</summary>
    private static int SaveBonus(Actor a, DndStat stat)
    {
        if (a.Pc is { } pc)
        {
            var mod = DndStatNames.Modifier(ItemStats.Effective(pc, stat));
            var proficient = Progression.ArchetypeOf(pc).Priority.Take(2).Contains(stat);
            return mod + (proficient ? ItemStats.Proficiency(pc) : 0);
        }

        return CombatRules.FoeSaveBonus(a.Foe!, stat);
    }

    private static readonly Regex PowerRx = new(@"^\s*(\d+(?:[.,]\d+)?)\s*[КK](?![\p{L}])\s*(.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex DiceRx = new(@"^\s*(\d*)\s*[dдк]\s*(\d+)\s*(?:([+\-−])\s*(\d+))?\s*(.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Кости действия: (кол-во, грани, бонус, тип). «1.5К огнём» → кости мощи владельца × 1.5 d8 + модификатор.</summary>
    private static (int Count, int Sides, int Bonus, string Type)? ParseAmount(string? raw, Actor who, int keyMod)
    {
        var text = (raw ?? "").Trim();
        if (text.Length == 0) return null;
        var power = PowerRx.Match(text);
        if (power.Success)
        {
            var n = double.Parse(power.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            var dice = Math.Max(1, (int)Math.Ceiling(n * Progression.PowerDice(who.Level)));
            return (Math.Min(dice, 40), 8, keyMod, power.Groups[2].Value.Trim());
        }

        var m = DiceRx.Match(text);
        if (!m.Success) return null;
        var count = m.Groups[1].Value.Length == 0 ? 1 : int.Parse(m.Groups[1].Value);
        var bonus = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) * (m.Groups[3].Value == "+" ? 1 : -1) : 0;
        return (Math.Clamp(count, 1, 40), Math.Clamp(int.Parse(m.Groups[2].Value), 2, 100), bonus, m.Groups[5].Value.Trim());
    }

    private static (int Total, string Text) RollPool(int count, int sides, int bonus, bool crit)
    {
        var n = crit ? count * 2 : count;
        var rolls = Enumerable.Range(0, n).Select(_ => Random.Shared.Next(1, sides + 1)).ToList();
        var total = Math.Max(0, rolls.Sum() + bonus);
        var text = $"{n}d{sides}[{string.Join(", ", rolls)}]" + (bonus != 0 ? $" {(bonus > 0 ? "+" : "−")} {Math.Abs(bonus)}" : "");
        return (total, text);
    }

    private static (int Face, string Text) RollD20(int advantage)
    {
        var first = Random.Shared.Next(1, 21);
        if (advantage == 0) return (first, $"d20 {first}");
        var second = Random.Shared.Next(1, 21);
        var face = advantage > 0 ? Math.Max(first, second) : Math.Min(first, second);
        return (face, $"d20 {first}/{second}→{face} ({(advantage > 0 ? L("преимущество", "advantage") : L("помеха", "disadvantage"))})");
    }

    private static int AdvParam(string? raw) => (raw ?? "").Trim().ToLowerInvariant() switch
    {
        "adv" or "advantage" or "преимущество" or "+" => 1,
        "dis" or "disadvantage" or "помеха" or "-" => -1,
        _ => 0,
    };

    // ===== resolve_attack =====

    private (bool Ok, string Result) ResolveAttack(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var state = _rpg.GetOrCreate(chatId);
        var attacker = FindActor(state, Str(args, "attacker"));
        if (attacker is null)
            return (false, L($"Ошибка: атакующий «{Str(args, "attacker")}» не найден среди героя, спутников и противников. Имена: {RosterText(state)}.", $"Error: the attacker \"{Str(args, "attacker")}\" was not found among the hero, companions and adversaries. Names: {RosterText(state)}."));
        if (attacker.Down)
            return (false, L($"Ошибка: {attacker.Name} выбыл из боя (ХП 0, бежал или сдался) и не может действовать.", $"Error: {attacker.Name} is out of the fight (HP 0, fled or surrendered) and cannot act."));
        if (CombatEffects.Read(attacker.Effects).SkipsTurn)
            return (false, L($"Ошибка: {attacker.Name} под эффектом, запрещающим действовать ({string.Join(", ", attacker.Effects.Where(CombatEffects.SkipsTurn))}). Пропусти его ход: combat_turn advance.", $"Error: {attacker.Name} is under an effect that forbids acting ({string.Join(", ", attacker.Effects.Where(CombatEffects.SkipsTurn))}). Skip their turn: combat_turn advance."));

        var mode = (Str(args, "mode") ?? "attack").Trim().ToLowerInvariant();
        if (mode is not ("attack" or "save" or "heal" or "shield" or "buff"))
            return (false, L("Ошибка: mode — attack, save, heal, shield или buff.", "Error: mode — attack, save, heal, shield or buff."));

        var targets = new List<Actor>();
        foreach (var part in (Str(args, "target") ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).Take(8))
        {
            var t = FindActor(state, part);
            if (t is null) return (false, L($"Ошибка: цель «{part}» не найдена. Имена: {RosterText(state)}.", $"Error: the target \"{part}\" was not found. Names: {RosterText(state)}."));
            targets.Add(t);
        }

        if (targets.Count == 0) return (false, L("Ошибка: укажи target — имя цели (или несколько через запятую).", "Error: specify target — the target's name (or several, comma-separated)."));
        if (mode is "attack" or "save" && !targets.Any(Targetable))
            return (false, L($"Ошибка: {string.Join(", ", targets.Select(t => t.Name))} уже выбыли из боя (повержены, мертвы, бежали или сдались) — выбери другую цель.", $"Error: {string.Join(", ", targets.Select(t => t.Name))} are already out of the fight (defeated, dead, fled or surrendered) — choose another target."));

        // Мана проверяется до броска: без неё навык не случается. Списывается после проверки остальных аргументов.
        var mana = Math.Max(0, Num(args, "mana") ?? 0);
        if (mana > 0 && attacker.IsPc)
        {
            var have = attacker.Hero?.ManaCurrent ?? attacker.Member!.ManaCurrent;
            if (have < mana) return (false, L($"Ошибка: у {attacker.Name} {have} маны, а навык стоит {mana}. Выбери другое действие.", $"Error: {attacker.Name} has {have} mana, and the skill costs {mana}. Choose another action."));
        }

        // Профиль атакующего: бонус атаки, кость урона, модификатор, DC.
        int atkBonus, keyMod, dc, defaultAttacks = 1;
        (int Count, int Sides, int Bonus, string Type) weaponDice;
        if (attacker.Pc is { } pc)
        {
            var weaponAtk = ItemStats.WeaponAttack(pc);
            var spellAtk = ItemStats.SpellAttack(pc);
            var useSpell = (Str(args, "weapon") ?? "").Trim().ToLowerInvariant() is "spell" or "заклинание" or "magic" or "program" && spellAtk is not null;
            var info = useSpell ? spellAtk! : weaponAtk;
            atkBonus = info.Attack;
            keyMod = Math.Max(weaponAtk.Bonus, spellAtk?.Bonus ?? int.MinValue);
            weaponDice = (info.Dice, info.Die, info.Bonus, info.Type);
            dc = ItemStats.SaveDc(pc);
        }
        else
        {
            var foe = attacker.Foe!;
            atkBonus = foe.AttackBonus;
            var (count, sides, type) = ItemStats.ParseDamage(foe.Damage);
            var bonusMatch = Regex.Match(foe.Damage ?? "", @"d\d+\s*([+\-−])\s*(\d+)");
            var bonus = bonusMatch.Success ? int.Parse(bonusMatch.Groups[2].Value) * (bonusMatch.Groups[1].Value == "+" ? 1 : -1) : 0;
            keyMod = bonus;
            weaponDice = (count, sides, bonus, type);
            dc = foe.SaveDc > 0 ? foe.SaveDc : 12;
            defaultAttacks = Math.Max(1, foe.Attacks);
        }

        if (Num(args, "dc") is { } dcArg && dcArg > 0) dc = dcArg;
        var amountRaw = Str(args, "damage");
        var explicitNone = (amountRaw ?? "").Trim().ToLowerInvariant() is "0" or "нет" or "none" or "-" or "—";
        var parsed = explicitNone ? null : ParseAmount(amountRaw, attacker, keyMod);
        if (amountRaw is { Length: > 0 } && parsed is null && !explicitNone)
            return (false, L($"Ошибка: не понимаю damage «{amountRaw}». Примеры: «1.5К огнём», «2К», «3d6 огонь», «2d8+3».", $"Error: I don't understand damage \"{amountRaw}\". Examples: \"1.5K fire\", \"2K\", \"3d6 fire\", \"2d8+3\"."));
        if (mode is "heal" or "shield" && parsed is null)
            return (false, L($"Ошибка: для mode={mode} передай damage — величину в костях мощи («1К», «2К») или костях («2d8+3»).", $"Error: for mode={mode} pass damage — an amount in power dice (\"1K\", \"2K\") or in dice (\"2d8+3\")."));
        if (mana > 0 && attacker.IsPc)
        {
            if (attacker.Hero is not null) attacker.Hero.ManaCurrent -= mana; else attacker.Member!.ManaCurrent -= mana;
        }

        var dice = parsed ?? weaponDice;
        var damageType = (Str(args, "damage_type") ?? "").Trim() is { Length: > 0 } dt ? dt : dice.Type;
        // Спасбросок без damage: у героя и спутника — чистый эффект (страх, оглушение), у противника — его статблок (дыхание, взрыв).
        var noDamage = explicitNone || (parsed is null && (mode == "buff" || (mode == "save" && attacker.IsPc)));

        var skill = (Str(args, "skill") ?? "").Trim();
        var effect = (Str(args, "effect") ?? "").Trim();
        var effectRounds = Num(args, "effect_rounds");
        var resist = (Str(args, "resist") ?? "").Trim().ToLowerInvariant();
        var attackerMods = CombatEffects.Read(attacker.Effects);
        var lines = new List<string>();
        var events = new List<CombatEvent>();
        var label = skill.Length > 0 ? skill : mode switch { "attack" => L("атака", "attack"), "save" => L("навык", "skill"), "heal" => L("лечение", "healing"), "shield" => L("щит", "shield"), _ => L("эффект", "effect") };
        var head = $"{attacker.Name} — {label}" + (mana > 0 ? $" (−{mana} {L("маны", "mana")})" : "");

        // ----- Лечение, щит, чистый эффект -----
        if (mode is "heal" or "shield" or "buff")
        {
            int amount = 0;
            var rollText = "";
            if (mode != "buff")
            {
                (amount, rollText) = RollPool(dice.Count, dice.Sides, dice.Bonus, false);
            }

            foreach (var t in targets)
            {
                var part = new List<string>();
                if (mode == "heal")
                {
                    var cap = t.Effects.Any(e => Regex.IsMatch(e, "некрот|порча|радиац|necrotic|blight|radiation", RegexOptions.IgnoreCase)) ? t.HpMax / 2 : t.HpMax;
                    var before = t.Hp;
                    var after = Math.Max(before, Math.Min(cap, before + amount));
                    SetHp(t, after);
                    part.Add($"+{after - before} {L("ХП", "HP")} ({before}→{after}/{t.HpMax}{(cap < t.HpMax ? L(", порча не даёт лечиться выше половины", ", the blight prevents healing above half") : "")})");
                    events.Add(new CombatEvent("heal", "✚", attacker.Name, t.Name, t.LogTarget, after - before, L($"+{after - before} жизни", $"+{after - before} HP"), rollText, null, false));
                }
                else if (mode == "shield")
                {
                    var capShield = Math.Max(1, t.HpMax / 2);
                    var value = Math.Min(capShield, Math.Max(t.Shield, amount));
                    SetShield(t, value);
                    part.Add($"{L("щит", "shield")} {value}{(amount > capShield ? L($" (потолок — половина ХП, {capShield})", $" (cap — half the HP, {capShield})") : "")}");
                    events.Add(new CombatEvent("shield", "🛡", attacker.Name, t.Name, t.LogTarget, value, $"{L("щит", "shield")} {value}", rollText, null, false));
                }

                if (effect.Length > 0)
                {
                    var applied = CombatEffects.AddOrRefresh(t.Effects, effect, effectRounds, state.InCombat());
                    part.Add(L($"эффект «{applied}»", $"effect \"{applied}\""));
                    events.Add(new CombatEvent("effect", "✦", attacker.Name, t.Name, t.LogTarget, null, L("эффект: ", "effect: ") + applied, null, null, false));
                }

                lines.Add($"- {t.Name}: {(part.Count > 0 ? string.Join(", ", part) : L("без изменений", "no changes"))}.");
            }

            _rpg.Save(chatId, state);
            CombatEvents.Push(chatId, events);
            return (true, $"OK: {head}{(rollText.Length > 0 ? $": {rollText} = {amount}" : "")}.\n{string.Join("\n", lines)}\n" + L("Состояние уже обновлено — не дублируй hp_delta/shield/эффекты.", "The state is already updated — do not duplicate hp_delta/shield/effects."));
        }

        // ----- Атака или спасбросок -----
        var damageMods = attackerMods.Damage;
        var attacks = Math.Clamp(Num(args, "attacks") ?? (mode == "attack" && parsed is null ? defaultAttacks : 1), 1, 4);
        var advArg = AdvParam(Str(args, "advantage"));
        var modifier = Num(args, "modifier") ?? 0;
        var cover = Math.Clamp(Num(args, "cover") ?? 0, 0, 10);
        var saveStat = ParseStat(Str(args, "save")) ?? DndStat.Dex;
        var halfOnSave = Flag(args, "half_on_save", true);
        var spentOnAttack = new List<string>();
        var hitAny = false;
        // Лежащего «при смерти» добивают только намеренно: если он уже лежал, когда мастер выбрал его целью.
        // Серия атак по стоящему герою не переходит сама на упавшего — оставшиеся удары уходят другим целям или пропадают.
        var downAtStart = targets.Where(t => t.IsPc && t.Hp <= 0).ToHashSet();
        bool Pickable(Actor x) => Targetable(x) && (!x.IsPc || x.Hp > 0 || downAtStart.Contains(x));
        var wasted = 0;

        // Урон области бросается один раз на всех, как в настольной игре.
        (int Total, string Text) saveDamage = noDamage || mode != "save" ? (0, "") : RollPool(dice.Count, dice.Sides, dice.Bonus + damageMods, false);
        if (mode == "save" && !noDamage) lines.Add(L("Урон области: ", "Area damage: ") + $"{saveDamage.Text} = {saveDamage.Total}{(damageType.Length > 0 ? " " + damageType : "")}.");

        for (var i = 0; i < (mode == "attack" ? attacks : targets.Count); i++)
        {
            var t = mode == "attack"
                ? targets.Skip(i % targets.Count).Concat(targets).FirstOrDefault(Pickable)
                : targets[i];
            if (t is null) { wasted = attacks - i; break; }
            if (mode == "save" && !Targetable(t)) continue;

            var tMods = CombatEffects.Read(t.Effects);
            var why = new List<string>();
            int dmg = 0;
            bool crit = false, hit;
            string rollLine;

            if (mode == "attack")
            {
                var adv = advArg;
                if (attackerMods.Adv) { adv++; why.Add(L("атакующий скрыт/точен", "the attacker is hidden/precise")); }
                if (attackerMods.Dis) { adv--; why.Add(L("атакующий ослаблен", "the attacker is weakened")); }
                if (tMods.GrantAdv) { adv++; why.Add(L("цель беспомощна", "the target is helpless")); }
                if (tMods.GrantDis) { adv--; why.Add(L("цель трудно задеть", "the target is hard to hit")); }
                if (t.IsPc && t.Hp <= 0 && !tMods.GrantAdv) { adv++; why.Add(L("цель без сознания", "the target is unconscious")); }
                adv = Math.Sign(adv);
                var (face, d20) = RollD20(adv);
                var bless = attackerMods.Bless ? Random.Shared.Next(1, 5) : 0;
                var total = face + atkBonus + attackerMods.Attack + modifier + bless;
                var ac = EffectiveAc(t) + cover;
                crit = face == 20;
                hit = crit || (face != 1 && total >= ac);
                rollLine = $"{d20} {Signed(atkBonus + attackerMods.Attack + modifier)}" +
                           (bless > 0 ? L(" + благословение ", " + blessing ") + bless : "") + L($" = {total} против КБ {ac}", $" = {total} vs AC {ac}") + (cover > 0 ? L($" (с укрытием +{cover})", $" (with cover +{cover})") : "");
                spentOnAttack.AddRange(CombatEffects.SpendOnAttack(attacker.Effects));
                if (hit)
                {
                    var (sum, text) = RollPool(dice.Count, dice.Sides, dice.Bonus + damageMods, crit);
                    dmg = sum;
                    rollLine += L("; урон ", "; damage ") + $"{text} = {sum}";
                }

                var verdict = crit ? L("крит!", "crit!") : face == 1 ? L("промах (натуральная 1)", "miss (natural 1)") : hit ? L("попадание", "hit") : L("промах", "miss");
                events.Add(new CombatEvent("attack", "⚔", attacker.Name, t.Name, t.LogTarget, total, total.ToString(), rollLine.Split(';')[0],
                    crit ? L("крит!", "crit!") : hit ? L("попадание", "hit") : L("промах", "miss"), !hit, crit, face == 1, null, label));
                rollLine = $"{verdict}: {rollLine}";
            }
            else
            {
                var adv = 0;
                if (tMods.CheckDis && saveStat == DndStat.Con) { adv--; why.Add(L("отравление — помеха на ТЕЛ", "poisoned — disadvantage on CON")); }
                if (tMods.Dis && saveStat == DndStat.Dex && tMods.GrantAdv) { adv--; why.Add(L("скован — помеха на ЛОВ", "restrained — disadvantage on DEX")); }
                var (face, d20) = RollD20(Math.Sign(adv));
                var bless = tMods.Bless ? Random.Shared.Next(1, 5) : 0;
                var bonus = SaveBonus(t, saveStat);
                var total = face + bonus + bless;
                var saved = total >= dc;
                hit = !saved;
                dmg = noDamage ? 0 : saved ? (halfOnSave ? saveDamage.Total / 2 : 0) : saveDamage.Total;
                rollLine = $"{L("спасбросок", "saving throw")} {DndStatNames.Short[(int)saveStat]}: {d20} {Signed(bonus)}{(bless > 0 ? L(" + благословение ", " + blessing ") + bless : "")} = {total} {L("против", "vs")} DC {dc} — " +
                           (saved ? (noDamage || !halfOnSave ? L("спасся", "saved") : L("спасся, половина урона", "saved, half damage")) : L("провал", "failed"));
                events.Add(new CombatEvent("save", "✦", attacker.Name, t.Name, t.LogTarget, total, total.ToString(),
                    $"{d20} {Signed(bonus)} = {total} {L("против СЛ", "vs DC")} {dc}", saved ? L("спасся", "saved") : L("провал", "failed"), saved, face == 20, face == 1, null,
                    L("Спасбросок ", "Saving throw ") + DndStatNames.Short[(int)saveStat]));
            }

            var parts = new List<string> { rollLine };
            if (dmg > 0 || (hit && !noDamage))
            {
                // Сопротивления: аргумент мастера (Bestiary.md) и эффекты цели.
                var mult = 1.0;
                var weak = resist is "weak" or "vulnerable" or "weakness" or "уязвим" or "слабость";
                if (resist is "immune" or "immunity" or "иммунитет") { mult = 0; why.Add(L("иммунитет", "immunity")); }
                else if (resist is "resist" or "resistance" or "resistant" or "сопротивление") { mult = 0.5; why.Add(L("сопротивление", "resistance")); }
                else if (tMods.ResistPhysical && CombatEffects.IsPhysical(damageType)) { mult = 0.5; why.Add(L("стойкость к физическому", "resistance to physical")); }
                if (mult > 0 && (weak || tMods.Vulnerable)) { mult *= 1.5; why.Add(weak ? L("слабость цели", "the target's weakness") : L("цель уязвима", "the target is vulnerable")); }
                var final = mult == 0 ? 0 : Math.Max(1, (int)Math.Floor(dmg * mult));
                var applied = ApplyDamage(state, t, final, crit);
                parts.Add($"{L("урон", "damage")} {final}{(damageType.Length > 0 ? " " + damageType : "")}{(final != dmg ? L($" (из {dmg})", $" (of {dmg})") : "")} → {applied.Text}");
                events.Add(new CombatEvent("damage", "🗡", attacker.Name, t.Name, t.LogTarget, -final, L($"{final} урона", $"{final} damage"), applied.Text, null, false));
                if (events.LastOrDefault(e => e.Kind == "attack" && e.Subject == t.Name) is { } atkEvent && mode == "attack") atkEvent.Damage = final;
                hitAny = true;

                // Стойкость элиты и боссов: крит или удар в слабость снимает сегмент, пустая шкала — «слом».
                if (t.Foe is { StaggerMax: > 0 } foe && foe.InFight && (crit || weak))
                {
                    foe.Stagger = Math.Max(0, (foe.Stagger > 0 ? foe.Stagger : foe.StaggerMax) - 1);
                    if (foe.Stagger == 0)
                    {
                        CombatEffects.AddOrRefresh(foe.Effects, L("Слом", "Break"), 1);
                        foe.Stagger = foe.StaggerMax;
                        parts.Add(L("СЛОМ! пропускает следующий ход и получает +50% урона до конца своего хода; шкала восстановлена", "BREAK! skips the next turn and takes +50% damage until the end of its turn; the bar is restored"));
                        events.Add(new CombatEvent("effect", "✦", attacker.Name, t.Name, t.LogTarget, null, L("СЛОМ", "BREAK"), null, null, false));
                    }
                    else
                    {
                        parts.Add(L("стойкость ", "poise ") + $"{foe.Stagger}/{foe.StaggerMax}");
                    }
                }
            }

            if (hit && effect.Length > 0 && Targetable(t) && t.Hp > 0)
            {
                var applied = CombatEffects.AddOrRefresh(t.Effects, effect, effectRounds, true);
                parts.Add(L($"эффект «{applied}»", $"effect \"{applied}\""));
                events.Add(new CombatEvent("effect", "✦", attacker.Name, t.Name, t.LogTarget, null, L("эффект: ", "effect: ") + applied, null, null, false));
            }

            if (why.Count > 0) parts.Add(L("учтено: ", "accounted for: ") + string.Join(", ", why.Distinct()));
            lines.Add($"- {t.Name}: {string.Join("; ", parts)}.");
        }

        if (wasted > 0) lines.Add(L($"Оставшиеся атаки ({wasted}) некуда направить — цель уже упала. Добить лежащего можно только отдельным осознанным действием.", $"The remaining attacks ({wasted}) have nowhere to go — the target is already down. A downed target can be finished only by a separate deliberate action."));
        if (spentOnAttack.Count > 0) lines.Add(L($"{attacker.Name} раскрылся: спало «{string.Join("», «", spentOnAttack.Distinct())}».", $"{attacker.Name} is revealed: \"{string.Join("\", \"", spentOnAttack.Distinct())}\" ended."));

        var morale = CombatRules.CheckMorale(state, events);
        lines.AddRange(morale);
        var over = !state.InCombat() && state.Adversaries.Count > 0 && targets.Any(t => t.Foe is not null);
        var endText = over ? CombatRules.EndCombat(state) : "";
        _rpg.Save(chatId, state);
        CombatEvents.Push(chatId, events);

        var sb = new StringBuilder();
        sb.Append($"OK: {head}.\n{string.Join("\n", lines)}\n");
        sb.Append(L("ХП, щиты и эффекты уже записаны — не дублируй их через update_*/roll_dice. Опиши действие живо, без формул.", "HP, shields and effects are already recorded — do not duplicate them via update_*/roll_dice. Describe the action vividly, without formulas."));
        if (over) sb.Append(L($"\nВсе противники выбыли — бой окончен. {endText}Вызови award_xp encounter=true, затем roll_loot; заверши ход через offer_choices.", $"\nAll adversaries are out — the fight is over. {endText}Call award_xp encounter=true, then roll_loot; finish the turn with offer_choices."));
        else if (!hitAny && mode == "attack") sb.Append(L(" Промах тоже двигает сцену: позиция, шум, контратака.", " A miss also moves the scene: position, noise, a counterattack."));
        return (true, sb.ToString().TrimEnd());
    }

    /// <summary>По цели ещё можно бить: противник в бою, герой или спутник не мёртв (лежащего «при смерти» добить можно).</summary>
    private static bool Targetable(Actor x) =>
        x.Foe is not null ? x.Foe.InFight : (x.Hero?.DeathState ?? x.Member!.DeathState) != "dead";

    private static string Signed(int value) => value >= 0 ? $"+ {value}" : $"− {-value}";

    private static string RosterText(RpgState state) =>
        string.Join(", ", new[] { "hero" }.Concat(state.Party.Select(p => p.Name)).Concat(state.Adversaries.Where(a => a.InFight).Select(a => a.Name)));

    private static DndStat? ParseStat(string? raw) => (raw ?? "").Trim().ToLowerInvariant() switch
    {
        "str" or "сил" or "сила" => DndStat.Str,
        "dex" or "лов" or "ловкость" => DndStat.Dex,
        "con" or "тел" or "телосложение" => DndStat.Con,
        "int" or "инт" or "интеллект" => DndStat.Int,
        "wis" or "мдр" or "мудрость" => DndStat.Wis,
        "cha" or "хар" or "харизма" => DndStat.Cha,
        _ => null,
    };

    private static void SetHp(Actor t, int hp)
    {
        if (t.Hero is { } h) { h.HpCurrent = hp; if (hp > 0) CombatRules.Revive(h); }
        else if (t.Member is { } m) { m.HpCurrent = hp; if (hp > 0) CombatRules.Revive(m); }
        else t.Foe!.HpCurrent = hp;
    }

    private static void SetShield(Actor t, int value)
    {
        if (t.Hero is { } h) h.Shield = value;
        else if (t.Member is { } m) m.Shield = value;
        else t.Foe!.Shield = value;
    }

    /// <summary>Урон с учётом щита и правил «при смерти». Возвращает строку итога для модели.</summary>
    private static (int Taken, string Text) ApplyDamage(RpgState state, Actor t, int damage, bool crit)
    {
        if (damage <= 0) return (0, $"{L("ХП", "HP")} {t.Hp}/{t.HpMax}");
        var shieldBefore = t.Shield;
        var absorbed = Math.Min(shieldBefore, damage);
        SetShield(t, shieldBefore - absorbed);
        var rest = damage - absorbed;
        var shieldText = absorbed > 0 ? L($"щит поглотил {absorbed}, ", $"the shield absorbed {absorbed}, ") : "";
        var before = t.Hp;

        if (t.Hero is { } hero)
        {
            if (rest > 0 && hero.HpCurrent == 0)
            {
                CombatRules.DamageWhileDying(hero);
                if (crit) CombatRules.DamageWhileDying(hero);
                return (rest, shieldText + L($"удар по лежащему: провалов спасброска {hero.DeathSaveFailures}/3{(hero.DeathState == "dead" ? " — ГЕРОЙ МЁРТВ" : "")}", $"a hit on the downed: death save failures {hero.DeathSaveFailures}/3{(hero.DeathState == "dead" ? " — THE HERO IS DEAD" : "")}"));
            }

            hero.HpCurrent = Math.Max(0, hero.HpCurrent - rest);
            if (hero.HpCurrent == 0) CombatRules.EnterDying(hero);
            return (rest, shieldText + $"{L("ХП", "HP")} {before}→{hero.HpCurrent}/{hero.HpMax}" + (hero.HpCurrent == 0 ? L(" — герой падает «при смерти» (resolve_death_save в начале его хода)", " — the hero falls \"dying\" (resolve_death_save at the start of their turn)") : ""));
        }

        if (t.Member is { } m)
        {
            if (rest > 0 && m.HpCurrent == 0)
            {
                CombatRules.DamageWhileDying(m);
                if (crit) CombatRules.DamageWhileDying(m);
                return (rest, shieldText + L($"удар по лежащему: провалов {m.DeathSaveFailures}/3{(m.DeathState == "dead" ? $" — {m.Name} мёртв" : "")}", $"a hit on the downed: failures {m.DeathSaveFailures}/3{(m.DeathState == "dead" ? $" — {m.Name} is dead" : "")}"));
            }

            m.HpCurrent = Math.Max(0, m.HpCurrent - rest);
            if (m.HpCurrent == 0) CombatRules.EnterDying(m);
            return (rest, shieldText + $"{L("ХП", "HP")} {before}→{m.HpCurrent}/{m.HpMax}" + (m.HpCurrent == 0 ? L($" — {m.Name} падает «при смерти»", $" — {m.Name} falls \"dying\"") : ""));
        }

        var foe = t.Foe!;
        foe.HpCurrent = Math.Max(0, foe.HpCurrent - rest);
        return (rest, shieldText + $"{L("ХП", "HP")} {before}→{foe.HpCurrent}/{foe.HpMax}" + (foe.HpCurrent == 0 ? L(" — повержен", " — defeated") : ""));
    }

    // ===== skill_check =====

    /// <summary>Навык → характеристика. Сюда же — ремёсла сеттингов (взлом, вождение, техника).</summary>
    private static readonly (string Stem, DndStat Stat, string Title)[] SkillMap =
    {
        ("атлет", DndStat.Str, "Атлетика"), ("сил", DndStat.Str, "Сила"), ("выбить", DndStat.Str, "Сила"), ("athlet", DndStat.Str, "Атлетика"),
        ("акробат", DndStat.Dex, "Акробатика"), ("скрыт", DndStat.Dex, "Скрытность"), ("stealth", DndStat.Dex, "Скрытность"),
        ("ловкость рук", DndStat.Dex, "Ловкость рук"), ("карман", DndStat.Dex, "Ловкость рук"), ("замк", DndStat.Dex, "Вскрытие замков"),
        ("вожден", DndStat.Dex, "Вождение"), ("пилот", DndStat.Dex, "Пилотирование"), ("лов", DndStat.Dex, "Ловкость"),
        ("вынослив", DndStat.Con, "Выносливость"), ("тел", DndStat.Con, "Телосложение"),
        ("маги", DndStat.Int, "Магия"), ("истори", DndStat.Int, "История"), ("природ", DndStat.Int, "Природа"), ("религ", DndStat.Int, "Религия"),
        ("анализ", DndStat.Int, "Анализ"), ("расслед", DndStat.Int, "Расследование"), ("взлом", DndStat.Int, "Взлом"), ("нетран", DndStat.Int, "Нетраннинг"),
        ("техник", DndStat.Int, "Техника"), ("кибертех", DndStat.Int, "Кибертехника"), ("алхим", DndStat.Int, "Алхимия"), ("инт", DndStat.Int, "Интеллект"),
        ("вниматель", DndStat.Wis, "Внимательность"), ("percep", DndStat.Wis, "Внимательность"), ("проницат", DndStat.Wis, "Проницательность"),
        ("выживан", DndStat.Wis, "Выживание"), ("медицин", DndStat.Wis, "Медицина"), ("животн", DndStat.Wis, "Обращение с животными"),
        ("чутьё", DndStat.Wis, "Уличное чутьё"), ("чутье", DndStat.Wis, "Уличное чутьё"), ("мдр", DndStat.Wis, "Мудрость"), ("мудр", DndStat.Wis, "Мудрость"),
        ("убежд", DndStat.Cha, "Убеждение"), ("обман", DndStat.Cha, "Обман"), ("запугив", DndStat.Cha, "Запугивание"), ("выступ", DndStat.Cha, "Выступление"),
        ("торг", DndStat.Cha, "Торг"), ("хар", DndStat.Cha, "Харизма"),
        // Английские названия навыков (английский интерфейс и мастер).
        ("acrobat", DndStat.Dex, "Acrobatics"), ("sleight", DndStat.Dex, "Sleight of Hand"), ("lock", DndStat.Dex, "Lockpicking"),
        ("driv", DndStat.Dex, "Driving"), ("pilot", DndStat.Dex, "Piloting"), ("endur", DndStat.Con, "Endurance"),
        ("arcan", DndStat.Int, "Arcana"), ("histor", DndStat.Int, "History"), ("nature", DndStat.Int, "Nature"), ("relig", DndStat.Int, "Religion"),
        ("investig", DndStat.Int, "Investigation"), ("hack", DndStat.Int, "Hacking"), ("netrun", DndStat.Int, "Netrunning"),
        ("tech", DndStat.Int, "Tech"), ("cybertech", DndStat.Int, "Cybertech"), ("alchem", DndStat.Int, "Alchemy"),
        ("insight", DndStat.Wis, "Insight"), ("surviv", DndStat.Wis, "Survival"), ("medic", DndStat.Wis, "Medicine"),
        ("animal", DndStat.Wis, "Animal Handling"), ("street", DndStat.Wis, "Streetwise"),
        ("persua", DndStat.Cha, "Persuasion"), ("decep", DndStat.Cha, "Deception"), ("intimid", DndStat.Cha, "Intimidation"),
        ("perform", DndStat.Cha, "Performance"), ("haggl", DndStat.Cha, "Haggling"), ("barter", DndStat.Cha, "Haggling"),
        ("strength", DndStat.Str, "Strength"), ("dexter", DndStat.Dex, "Dexterity"), ("constit", DndStat.Con, "Constitution"),
        ("intell", DndStat.Int, "Intelligence"), ("wisdom", DndStat.Wis, "Wisdom"), ("charism", DndStat.Cha, "Charisma"),
    };

    private static (DndStat Stat, string Title)? ResolveSkill(string? raw)
    {
        var text = (raw ?? "").Trim();
        if (ParseStat(text) is { } st) return (st, DndStatNames.Short[(int)st]);
        var lower = text.ToLowerInvariant();
        foreach (var (stem, stat, title) in SkillMap)
        {
            if (lower.StartsWith(stem, StringComparison.Ordinal) || lower.Contains(" " + stem, StringComparison.Ordinal))
                return (stat, text.Length > 0 ? char.ToUpperInvariant(text[0]) + text[1..] : title);
        }

        return null;
    }

    private (bool Ok, string Result) SkillCheck(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var state = _rpg.GetOrCreate(chatId);
        var who = FindActor(state, Str(args, "who") ?? "hero");
        if (who?.Pc is not { } pc) return (false, L($"Ошибка: «{Str(args, "who")}» — не герой и не спутник. Проверки противников бросай roll_dice.", $"Error: \"{Str(args, "who")}\" is neither the hero nor a companion. Roll adversary checks with roll_dice."));
        if (who.Hp <= 0) return (false, L($"Ошибка: {who.Name} без сознания и не может действовать.", $"Error: {who.Name} is unconscious and cannot act."));
        if (ResolveSkill(Str(args, "skill")) is not { } sk)
            return (false, L("Ошибка: не понимаю skill. Передай навык (Атлетика, Скрытность, Внимательность, Убеждение…) или характеристику (str/dex/con/int/wis/cha).", "Error: I don't understand skill. Pass a skill (Athletics, Stealth, Perception, Persuasion…) or an ability (str/dex/con/int/wis/cha)."));
        var dc = Num(args, "dc") ?? 0;
        if (dc <= 0) return (false, L("Ошибка: укажи dc — Сложность проверки (5–30).", "Error: specify dc — the check's difficulty (5–30)."));

        var isSave = Flag(args, "save");
        var mods = CombatEffects.Read(who.Effects);
        var adv = AdvParam(Str(args, "advantage"));
        var why = new List<string>();
        if (mods.CheckDis || (mods.Dis && isSave && sk.Stat == DndStat.Dex)) { adv--; why.Add(L("эффект на персонаже — помеха", "an effect on the character — disadvantage")); }
        adv = Math.Sign(adv);

        int face;
        string d20;
        if (Num(args, "roll") is { } rolled && rolled is >= 1 and <= 20)
        {
            face = rolled;
            d20 = L($"d20 игрока {rolled}", $"the player's d20 {rolled}");
            if (adv != 0)
            {
                // Игрок бросил один кубик — второй для преимущества/помехи бросает харнес.
                var second = Random.Shared.Next(1, 21);
                face = adv > 0 ? Math.Max(rolled, second) : Math.Min(rolled, second);
                d20 = L($"d20 игрока {rolled} и второй {second}→{face} ({(adv > 0 ? "преимущество" : "помеха")})", $"the player's d20 {rolled} and a second {second}→{face} ({(adv > 0 ? "advantage" : "disadvantage")})");
            }
        }
        else
        {
            (face, d20) = RollD20(adv);
        }

        var statMod = DndStatNames.Modifier(ItemStats.Effective(pc, sk.Stat));
        var prof = Flag(args, "proficient") ? ItemStats.Proficiency(pc) : 0;
        var bless = isSave && mods.Bless ? Random.Shared.Next(1, 5) : 0;
        var bonus = Num(args, "bonus") ?? 0;
        var total = face + statMod + prof + bonus + bless;
        var margin = total - dc;

        // Степени успеха: натуральные 20 и 1 сдвигают итог на ступень.
        var degree = margin >= 10 ? 3 : margin >= 0 ? 2 : margin >= -3 ? 1 : 0;
        if (face == 20) degree = Math.Min(3, degree + 1);
        if (face == 1) degree = Math.Max(0, degree - 1);
        if (isSave && degree == 1) degree = 0; // у спасброска нет «успеха ценой»: устоял или нет
        var verdict = degree switch
        {
            3 => L("БЛЕСТЯЩИЙ УСПЕХ — цель достигнута с дополнительной выгодой (время, сведения, впечатление, позиция)", "BRILLIANT SUCCESS — the goal is achieved with an extra benefit (time, information, impression, position)"),
            2 => L("УСПЕХ", "SUCCESS"),
            1 => L("УСПЕХ ЦЕНОЙ — цель достигнута, но с осложнением: назови цену (шум, время, вещь, ранение, ухудшение позиции) и дай игроку согласиться или отступить", "SUCCESS AT A COST — the goal is achieved, but with a complication: name the cost (noise, time, an item, a wound, a worse position) and let the player agree or back off"),
            _ => face == 1
                ? L("ПРОВАЛ С ОСЛОЖНЕНИЕМ — не вышло, и ситуация ухудшилась; но провал двигает историю вперёд", "FAILURE WITH A COMPLICATION — it did not work, and the situation got worse; but failure moves the story forward")
                : L("ПРОВАЛ — не вышло; покажи последствия и новую возможность, а не тупик", "FAILURE — it did not work; show the consequences and a new opportunity, not a dead end"),
        };

        var math = $"{d20} {(statMod >= 0 ? "+" : "−")} {DndStatNames.Short[(int)sk.Stat]} {Math.Abs(statMod)}" +
                   (prof > 0 ? L(" + мастерство ", " + proficiency ") + prof : "") + (bonus != 0 ? $" {(bonus > 0 ? "+" : "−")} {L("поправка", "modifier")} {Math.Abs(bonus)}" : "") +
                   (bless > 0 ? L(" + благословение ", " + blessing ") + bless : "") + $" = {total} {L("против", "vs")} DC {dc}";
        var reason = (Str(args, "reason") ?? "").Trim();
        if (state.InCombat())
        {
            CombatEvents.Push(chatId, new List<CombatEvent>
            {
                new(isSave ? "save" : "roll", isSave ? "✦" : "🎲", who.Name, null, who.LogTarget, total, total.ToString(), math,
                    degree >= 2 ? L("успех", "success") : degree == 1 ? L("ценой", "at a cost") : L("провал", "failure"), degree == 0, face == 20, face == 1, null,
                    $"{(isSave ? L("Спасбросок", "Saving throw") : L("Проверка", "Check"))}: {sk.Title}", $"{who.Name}: {sk.Title}"),
            });
        }

        return (true, $"{(isSave ? L("Спасбросок", "Saving throw") : L("Проверка", "Check"))} {L("«", "\"")}{sk.Title}{L("»", "\"")} — {who.Name}{(reason.Length > 0 ? $" ({reason})" : "")}: {math} — {verdict}." +
                      (why.Count > 0 ? L(" Учтено: ", " Accounted for: ") + string.Join(", ", why) + "." : "") +
                      L(" Опиши исход в сцене; формулу игроку не пересказывай.", " Describe the outcome in the scene; do not retell the formula to the player."));
    }

    // ===== Время =====

    private static string[] DayParts => Lang.IsEn ? new[] { "morning", "day", "evening", "night" } : new[] { "утро", "день", "вечер", "ночь" };

    public static string TimeText(RpgState state) => $"{Lang.T("день", "day")} {Math.Max(1, state.Day)}, {DayParts[Math.Clamp(state.DayPart, 0, 3)]}";

    private static int? ParseDayPart(string? raw) => (raw ?? "").Trim().ToLowerInvariant() switch
    {
        "утро" or "morning" or "рассвет" or "dawn" => 0,
        "день" or "day" or "полдень" or "noon" or "afternoon" => 1,
        "вечер" or "evening" or "закат" or "dusk" => 2,
        "ночь" or "night" or "полночь" or "midnight" => 3,
        _ => null,
    };

    /// <summary>
    /// Сдвинуть время на дни и части суток; часы с per_days тикают сами. Возвращает сообщения о заполненных часах
    /// (их надо донести до мастера в результате инструмента — это события мира).
    /// </summary>
    private static List<string> AdvanceTime(RpgState state, int days, int parts)
    {
        state.Day = Math.Max(1, state.Day);
        var totalParts = state.DayPart + Math.Max(0, parts);
        state.Day += Math.Max(0, days) + totalParts / 4;
        state.DayPart = totalParts % 4;
        return TickClocksByDays(state);
    }

    private static List<string> TickClocksByDays(RpgState state)
    {
        var news = new List<string>();
        foreach (var clock in state.Clocks.Where(c => c.PerDays > 0 && !c.Done))
        {
            if (clock.LastTickDay <= 0) clock.LastTickDay = state.Day;
            while (!clock.Done && state.Day - clock.LastTickDay >= clock.PerDays)
            {
                clock.LastTickDay += clock.PerDays;
                clock.Filled = Math.Min(clock.Segments, clock.Filled + 1);
                if (clock.Filled >= clock.Segments) clock.Done = true;
            }

            if (clock.Done) news.Add(ClockFilledText(clock));
            else if (clock.Filled >= clock.Segments - 1) news.Add(L($"⏳ Часы «{clock.Name}» — {clock.Filled}/{clock.Segments}: до события один шаг. Покажи приметы надвигающегося.", $"⏳ The clock \"{clock.Name}\" — {clock.Filled}/{clock.Segments}: one step until the event. Show signs of what is coming."));
        }

        return news;
    }

    private static string ClockFilledText(StoryClock clock) =>
        L($"⏰ Часы «{clock.Name}» заполнились ({clock.Segments}/{clock.Segments}){(clock.OnFill.Length > 0 ? $": {clock.OnFill}" : "")}. " +
        "Событие произошло — отрази его в мире (update_world_event, сцена, слухи, Secrets.md) и, если угроза продолжится, заведи новые часы.",
        $"⏰ The clock \"{clock.Name}\" is full ({clock.Segments}/{clock.Segments}){(clock.OnFill.Length > 0 ? $": {clock.OnFill}" : "")}. " +
        "The event has happened — reflect it in the world (update_world_event, a scene, rumors, Secrets.md) and, if the threat continues, set up new clocks.");

    // ===== rest =====

    private static readonly string[] FoodStems =
        { "паёк", "паек", "пайк", "рацион", "еда", "хлеб", "сыр", "мясо", "вялен", "сухар", "консерв", "батончик", "провиз", "лепёш", "лепеш", "сухпа", "яблок", "колбас", "орех",
          "ration", "food", "bread", "cheese", "meat", "jerky", "hardtack", "canned", "bar", "provision", "flatbread", "mre", "apple", "sausage", "nut" };

    private (bool Ok, string Result) Rest(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var state = _rpg.GetOrCreate(chatId);
        if (state.InCombat()) return (false, L("Ошибка: в бою не отдыхают. Сначала закончи бой (победа, бегство, переговоры).", "Error: there is no resting in combat. First end the fight (victory, flight, negotiations)."));

        var kind = (Str(args, "kind") ?? "").Trim().ToLowerInvariant() switch
        {
            "short" or "короткий" or "привал" => "short",
            "long" or "долгий" or "ночлег" or "сон" or "sleep" or "night" => "long",
            _ => "",
        };
        if (kind.Length == 0) return (false, L("Ошибка: kind — short (привал) или long (ночлег).", "Error: kind — short (a halt) or long (a night's rest)."));
        var place = (Str(args, "place") ?? "safe").Trim().ToLowerInvariant() switch
        {
            "wild" or "дикая" or "лагерь" or "лес" or "camp" or "forest" => "wild",
            "hostile" or "враждебная" or "данж" or "dungeon" => "hostile",
            _ => "safe",
        };
        // Продолжить можно только отдых, прерванный тревогой, — иначе resume стал бы бесплатным вторым ночлегом.
        var resume = Flag(args, "resume") && state.PendingRest == kind;
        state.PendingRest = "";

        if (kind == "short" && state.ShortRests >= 2)
            return (false, L("Ошибка: два коротких привала уже было — дальше нужен ночлег (rest kind=long).", "Error: there have already been two short halts — a night's rest is needed next (rest kind=long)."));
        if (kind == "long" && state.LastLongRestDay == state.Day && state.DayPart == 0 && !resume)
            return (false, L("Ошибка: группа только что выспалась. Новый ночлег — после дня приключений; если прошло время, сдвинь его через set_world_state (day_part).", "Error: the party has just slept. A new night's rest — after a day of adventures; if time has passed, move it via set_world_state (day_part)."));

        var sb = new StringBuilder();

        // Ночная тревога: чем опаснее место, тем вероятнее. Прерванный отдых ничего не даёт, пока тревогу не разыграют.
        if (!resume && place != "safe")
        {
            var chance = (kind, place) switch { ("long", "wild") => 25, ("long", _) => 50, (_, "wild") => 10, _ => 25 };
            var d100 = Random.Shared.Next(1, 101);
            if (d100 <= chance)
            {
                var alarm = PickAlarm(place);
                state.PendingRest = kind;
                _rpg.Save(chatId, state);
                return (true, L($"🌙 Тревога на {(kind == "long" ? "ночлеге" : "привале")}: d100 {d100} ≤ {chance} — отдых прерван. {alarm} " +
                              "Разыграй тревогу (бой через plan_encounter, проверка через skill_check или разговор). Когда опасность позади, " +
                              "вызови rest снова с resume=true — восстановление начислится тогда. Если герой уходит — отдыха нет.",
                              $"🌙 An alarm during the {(kind == "long" ? "night's rest" : "halt")}: d100 {d100} ≤ {chance} — the rest is interrupted. {alarm} " +
                              "Play out the alarm (a fight via plan_encounter, a check via skill_check or a conversation). When the danger is past, " +
                              "call rest again with resume=true — the recovery is applied then. If the hero leaves, there is no rest."));
            }

            sb.Append(L($"Тревоги не было (d100 {d100} > {chance}). ", $"No alarm (d100 {d100} > {chance}). "));
        }

        // Еда в походе: ночлег вне поселения съедает паёк; без еды — половина восстановления.
        var share = kind == "long" ? 1.0 : 0.25;
        if (kind == "long" && place != "safe")
        {
            var food = state.Grid.FirstOrDefault(i => FoodStems.Any(f => i.Name.Contains(f, StringComparison.OrdinalIgnoreCase)));
            if (food is null)
            {
                share = 0.5;
                sb.Append(L("Еды в сумке нет — голодный ночлег: восстановлена лишь половина. ", "No food in the bag — a hungry night: only half is restored. "));
            }
            else
            {
                if (food.Quantity > 1) food.Quantity--; else state.Grid.Remove(food);
                sb.Append(L($"Съеден «{food.Name}»{(food.Quantity > 0 && state.Grid.Contains(food) ? $" (осталось {food.Quantity})" : " (последний)")}. ", $"Eaten: \"{food.Name}\"{(food.Quantity > 0 && state.Grid.Contains(food) ? $" ({food.Quantity} left)" : " (the last one)")}. "));
            }
        }

        var report = new List<string>();
        void Recover(string name, Func<int> hp, Action<int> setHp, int hpMax, Func<int> mana, Action<int> setMana, int manaMax,
            Func<string> death, Action<string> setDeath, List<string> status, Action<int> setShield)
        {
            if (death() == "dead") { report.Add(L($"{name} мёртв — отдых не поможет", $"{name} is dead — rest will not help")); return; }
            var hpBefore = hp();
            var manaBefore = mana();
            var startHp = hpBefore <= 0 ? (death() == "stable" || kind == "long" ? 1 : 0) : hpBefore;
            var newHp = startHp <= 0 ? 0 : Math.Min(hpMax, startHp + (int)Math.Ceiling(hpMax * share));
            if (kind == "long" && share >= 1) newHp = hpMax;
            var newMana = Math.Min(manaMax, manaBefore + (int)Math.Ceiling(manaMax * share));
            setHp(newHp);
            setMana(newMana);
            setShield(0);
            if (newHp > 0) setDeath("");
            var cleared = CombatEffects.ClearAfterRest(status);
            report.Add($"{name}: {L("ХП", "HP")} {hpBefore}→{newHp}/{hpMax}" + (manaMax > 0 ? $", {L("мана", "mana")} {manaBefore}→{newMana}/{manaMax}" : "") +
                       (cleared.Count > 0 ? L(", прошло: ", ", ended: ") + string.Join(", ", cleared) : ""));
        }

        var c = state.Character;
        Recover(c.Name.Length > 0 ? c.Name : L("Герой", "Hero"), () => c.HpCurrent, v => c.HpCurrent = v, c.HpMax, () => c.ManaCurrent, v => c.ManaCurrent = v, c.ManaMax,
            () => c.DeathState, v => { c.DeathState = v; c.DeathSaveFailures = c.DeathSaveSuccesses = 0; }, c.Status, v => c.Shield = v);
        foreach (var m in state.Party)
        {
            var member = m;
            Recover(member.Name, () => member.HpCurrent, v => member.HpCurrent = v, member.HpMax, () => member.ManaCurrent, v => member.ManaCurrent = v, member.ManaMax,
                () => member.DeathState, v => { member.DeathState = v; member.DeathSaveFailures = member.DeathSaveSuccesses = 0; }, member.Status, v => member.Shield = v);
        }

        var news = new List<string>();
        if (kind == "long")
        {
            state.ShortRests = 0;
            news = AdvanceTime(state, 1, 0);
            state.DayPart = 0;
            state.LastLongRestDay = state.Day;
        }
        else
        {
            state.ShortRests++;
        }

        _rpg.Save(chatId, state);
        sb.Append(kind == "long"
            ? L($"Ночлег ({place switch { "wild" => "лагерь", "hostile" => "опасное место", _ => "под крышей" }}): наступило утро — {TimeText(state)}.\n",
                $"A night's rest ({place switch { "wild" => "camp", "hostile" => "a dangerous place", _ => "under a roof" }}): morning has come — {TimeText(state)}.\n")
            : L($"Короткий привал (≈1 час), привалов с ночлега: {state.ShortRests}/2.\n", $"A short halt (≈1 hour), halts since the last night's rest: {state.ShortRests}/2.\n"));
        sb.Append(string.Join("\n", report.Select(r => "- " + r)));
        if (kind == "long") sb.Append(L("\nСвойства вещей и навыков «раз в день» снова доступны.", "\n\"Once a day\" item and skill properties are available again."));
        if (news.Count > 0) sb.Append("\n" + string.Join("\n", news));
        sb.Append(L("\nВосстановление уже записано — опиши отдых в 1–3 фразах (сны, разговоры у огня, погода) и продолжай.", "\nThe recovery is already recorded — describe the rest in 1–3 sentences (dreams, talk by the fire, weather) and continue."));
        return (true, sb.ToString());
    }

    private static string PickAlarm(string place)
    {
        var options = Lang.IsEn
            ? (place == "hostile"
                ? Genre.Pick(
                    new[] { "A patrol's footsteps come closer.", "Creatures drawn by the smell crawl out of the darkness.", "Someone is trying to rob the sleepers.", "A trap or a collapse shakes the shelter." },
                    new[] { "The scanner picks up a patrol drone.", "A gang is sweeping the floor.", "Someone is forcing the shelter door.", "The neighboring block's alarm goes off." },
                    new[] { "Footsteps and flashlights near the shelter.", "Strangers are checking the building.", "Someone is breaking into the car.", "A call from an unknown number — they are looking for you." })
                : Genre.Pick(
                    new[] { "A predator creeps toward the camp.", "A wanderer appears at the fire — a friend or a spy?", "Bandits have noticed the fire.", "A storm hits: the wind tears the shelter away." },
                    new[] { "Wasteland scavengers come to the fire.", "A pack of mutants smells prey.", "A strange drone hovers over the camp.", "Acid rain — shelter is needed right now." },
                    new[] { "A bear comes up to the camp.", "A car with its headlights off pulls up.", "Locals noticed the tent and are coming to sort things out.", "A thunderstorm: the tent is blown down, the gear gets wet." }))
            : place == "hostile"
            ? Genre.Pick(
                new[] { "Шаги патруля всё ближе.", "Из темноты выползают твари, привлечённые запахом.", "Кто-то пытается обокрасть спящих.", "Ловушка или обвал сотрясает укрытие." },
                new[] { "Сканер ловит патрульный дрон.", "Банда прочёсывает этаж.", "Кто-то вскрывает дверь укрытия.", "Срабатывает сигнализация соседнего блока." },
                new[] { "Шаги и фонари у укрытия.", "Чужие проверяют здание.", "Кто-то лезет к машине.", "Звонок с незнакомого номера — вас ищут." })
            : Genre.Pick(
                new[] { "Хищник крадётся к лагерю.", "У костра появляется странник — друг или лазутчик?", "Разбойники заметили огонь.", "Налетает буря: укрытие срывает ветром." },
                new[] { "К костру выходят мусорщики Пустошей.", "Стая мутантов чует добычу.", "Над лагерем зависает чужой дрон.", "Кислотный дождь — нужно срочно укрытие." },
                new[] { "К лагерю выходит медведь.", "Подъезжает машина с потушенными фарами.", "Местные заметили палатку и идут разбираться.", "Гроза: палатку сносит, вещи мокнут." });
        return options[Random.Shared.Next(options.Length)];
    }

    // ===== update_clock =====

    private (bool Ok, string Result) UpdateClock(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var state = _rpg.GetOrCreate(chatId);
        var name = (Str(args, "name") ?? "").Trim();
        if (name.Length == 0) return (false, L("Ошибка: укажи name — название часов.", "Error: specify name — the clock's name."));
        var clock = state.Clocks.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (Flag(args, "remove"))
        {
            if (clock is null) return (false, L($"Ошибка: часов «{name}» нет.", $"Error: there is no clock \"{name}\"."));
            state.Clocks.Remove(clock);
            _rpg.Save(chatId, state);
            return (true, L($"OK: часы «{name}» убраны.", $"OK: the clock \"{name}\" is removed."));
        }

        var created = clock is null;
        if (clock is null)
        {
            clock = new StoryClock { Name = name, LastTickDay = Math.Max(1, state.Day) };
            state.Clocks.Add(clock);
            if (state.Clocks.Count > 12) return (false, L("Ошибка: часов слишком много (12). Закрой исполнившиеся или устранённые через remove.", "Error: too many clocks (12). Close the fulfilled or eliminated ones via remove."));
        }

        if (Num(args, "segments") is { } seg) clock.Segments = Math.Clamp(seg, 2, 12);
        if (Str(args, "kind") is { Length: > 0 } kind)
            clock.Kind = kind.Trim().ToLowerInvariant() switch { "progress" or "прогресс" => "progress", "rival" or "соперник" => "rival", _ => "threat" };
        if (args["secret"] is not null) clock.Secret = Flag(args, "secret");
        else if (created) clock.Secret = clock.Kind == "threat";
        if (Num(args, "per_days") is { } per) { clock.PerDays = Math.Clamp(per, 0, 60); clock.LastTickDay = Math.Max(1, state.Day); }
        if (Str(args, "on_fill") is { Length: > 0 } fill) clock.OnFill = fill.Trim();
        if (Flag(args, "reset")) { clock.Filled = 0; clock.Done = false; clock.LastTickDay = Math.Max(1, state.Day); }

        var news = "";
        if (Num(args, "tick") is { } tick && tick != 0)
        {
            if (clock.Done && tick > 0) return (false, L($"Ошибка: часы «{clock.Name}» уже заполнены. Сбрось их (reset) или заведи новые.", $"Error: the clock \"{clock.Name}\" is already full. Reset it (reset) or set up a new one."));
            clock.Filled = Math.Clamp(clock.Filled + tick, 0, clock.Segments);
            if (clock.Filled >= clock.Segments)
            {
                clock.Done = true;
                news = " " + ClockFilledText(clock);
            }
            else
            {
                clock.Done = false;
            }
        }

        _rpg.Save(chatId, state);
        var kindTitle = clock.Kind switch { "progress" => L("прогресс", "progress"), "rival" => L("соперник", "rival"), _ => L("угроза", "threat") };
        return (true, L($"OK: часы «{clock.Name}» {(created ? "заведены" : "обновлены")}: ", $"OK: the clock \"{clock.Name}\" is {(created ? "set up" : "updated")}: ") +
                      $"{clock.Filled}/{clock.Segments} [{kindTitle}{(clock.Secret ? L(", тайные", ", secret") : L(", видны игроку", ", visible to the player"))}" +
                      (clock.PerDays > 0 ? L($", +1 каждые {clock.PerDays} дн.", $", +1 every {clock.PerDays} d.") : "") + "]" +
                      (clock.OnFill.Length > 0 ? L("; по заполнении: ", "; when full: ") + clock.OnFill : created ? L("; задай on_fill — что случится по заполнении", "; set on_fill — what happens when it fills") : "") + "." + news);
    }

    // ===== update_faction =====

    private (bool Ok, string Result) UpdateFaction(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var state = _rpg.GetOrCreate(chatId);
        var name = (Str(args, "name") ?? "").Trim();
        if (name.Length == 0) return (false, L("Ошибка: укажи name — название фракции.", "Error: specify name — the faction's name."));
        var faction = state.Factions.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (Flag(args, "remove"))
        {
            if (faction is null) return (false, L($"Ошибка: фракции «{name}» нет в списке.", $"Error: the faction \"{name}\" is not in the list."));
            state.Factions.Remove(faction);
            _rpg.Save(chatId, state);
            return (true, L($"OK: фракция «{name}» убрана.", $"OK: the faction \"{name}\" is removed."));
        }

        var created = faction is null;
        faction ??= new FactionStanding { Name = name };
        if (created) state.Factions.Add(faction);
        var before = faction.Standing;
        var beforeLabel = FactionStanding.Label(before);
        if (Num(args, "set") is { } set) faction.Standing = Math.Clamp(set, -100, 100);
        if (Num(args, "delta") is { } delta) faction.Standing = Math.Clamp(faction.Standing + Math.Clamp(delta, -40, 40), -100, 100);
        if (Str(args, "note") is { Length: > 0 } note) faction.Note = note.Trim();
        var reason = (Str(args, "reason") ?? "").Trim();
        if (reason.Length > 0 && faction.Standing != before)
        {
            faction.History.Add($"{TimeText(state)}: {(faction.Standing - before):+#;-#} — {reason}");
            if (faction.History.Count > 5) faction.History.RemoveRange(0, faction.History.Count - 5);
        }

        _rpg.Save(chatId, state);
        var label = FactionStanding.Label(faction.Standing);
        var shift = label != beforeLabel && !created
            ? L($" Отношение сменилось: {beforeLabel} → {label} — покажи это в мире (встреча с членом фракции, слух, письмо, засада или помощь).", $" The attitude changed: {beforeLabel} → {label} — show it in the world (a meeting with a faction member, a rumor, a letter, an ambush or help).")
            : "";
        return (true, $"OK: {L("«", "\"")}{faction.Name}{L("»", "\"")} — {label} ({faction.Standing:+#;-#;0}){(created ? L(", фракция заведена", ", the faction is set up") : L($", было {before:+#;-#;0}", $", was {before:+#;-#;0}"))}.{shift}");
    }

    /// <summary>Сводка для промпта: время, часы, фракции, отдых.</summary>
    private static void AppendWorldClock(StringBuilder sb, RpgState state)
    {
        sb.AppendLine(L($"- Время: {TimeText(state)}; привалов с ночлега {state.ShortRests}/2. ХП и ману восстанавливает только rest.", $"- Time: {TimeText(state)}; halts since the last night's rest {state.ShortRests}/2. Only rest restores HP and mana."));
        var clocks = state.Clocks.Where(c => !c.Done).Select(c =>
            $"{L("«", "\"")}{c.Name}{L("»", "\"")} {c.Filled}/{c.Segments} [{(c.Kind switch { "progress" => L("прогресс", "progress"), "rival" => L("соперник", "rival"), _ => L("угроза", "threat") })}{(c.Secret ? L(", тайные", ", secret") : "")}" +
            (c.PerDays > 0 ? L($", +1/{c.PerDays} дн.", $", +1/{c.PerDays} d.") : "") + "]" + (c.OnFill.Length > 0 ? $" → {c.OnFill}" : "")).ToList();
        if (clocks.Count > 0) sb.AppendLine(L($"- Часы: {string.Join("; ", clocks)}. Тикай их update_clock, когда сцена даёт повод (шум, провал, промедление, успех соперника).", $"- Clocks: {string.Join("; ", clocks)}. Tick them with update_clock when the scene gives a reason (noise, a failure, a delay, a rival's success)."));
        else if (state.StoryArcs.Any(a => a.State == "active"))
            sb.AppendLine(L("- Часы: нет. Заведи 1–2 часа угрозы активной арки (update_clock, secret=true, per_days, on_fill) — антагонист действует, пока герой занят другим.", "- Clocks: none. Set up 1–2 threat clocks for the active arc (update_clock, secret=true, per_days, on_fill) — the antagonist acts while the hero is busy with other things."));
        var factions = state.Factions.Select(f => $"{f.Name} — {FactionStanding.Label(f.Standing)} ({f.Standing:+#;-#;0})").ToList();
        if (factions.Count > 0) sb.AppendLine(L("- Фракции: ", "- Factions: ") + string.Join("; ", factions) + ".");
    }
}

/// <summary>Событие для журнала боя от инструментов, которые сами бросают кости (атака, тик эффектов, проверка).</summary>
public sealed record CombatEvent(string Kind, string Icon, string? Actor, string? Subject, string? Target, int? Amount, string? Value,
    string? Detail, string? Verdict, bool VerdictBad, bool Crit = false, bool Fail = false, int? Damage = null, string? Label = null, string Text = "")
{
    public int? Damage { get; set; } = Damage;
}

/// <summary>
/// Буфер событий между выполнением инструмента и журналом боя: FileTools кладёт сюда, BattleLogFeed забирает
/// сразу после вызова. Буфер чистится в начале каждого вызова — события не переживают свой инструмент.
/// </summary>
public static class CombatEvents
{
    private static readonly ConcurrentDictionary<string, List<CombatEvent>> Pending = new();

    public static void Push(string chatId, IEnumerable<CombatEvent> events)
    {
        var list = Pending.GetOrAdd(chatId, _ => new List<CombatEvent>());
        lock (list)
        {
            list.AddRange(events);
            if (list.Count > 200) list.RemoveRange(0, list.Count - 200);
        }
    }

    public static List<CombatEvent> Drain(string chatId)
    {
        if (!Pending.TryGetValue(chatId, out var list)) return new List<CombatEvent>();
        lock (list)
        {
            var copy = list.ToList();
            list.Clear();
            return copy;
        }
    }
}
