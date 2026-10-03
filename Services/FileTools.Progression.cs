using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace RPG_Harness.Services;

/// <summary>
/// Инструменты прогрессии и баланса: план встречи с честными статблоками (plan_encounter), опыт (award_xp),
/// повышение уровня (level_up), генерация добычи (roll_loot) и найм спутника с выбором роли
/// (update_companion_candidate → ask_companion_role → recruit_companion). Числа считает харнес, модель отвечает за историю.
/// </summary>
public sealed partial class FileTools
{
    private static readonly string[] ProgressionTools =
        { "plan_encounter", "award_xp", "level_up", "roll_loot", "update_companion_candidate", "ask_companion_role", "recruit_companion" };

    private static JsonObject Prop(string type, string description) => new() { ["type"] = type, ["description"] = description };

    private IEnumerable<JsonObject> ExtraDefinitions()
    {
        yield return RawDef(
            "plan_encounter",
            L("Спланировать бой по честной формуле баланса и (по умолчанию) сразу вывести противников в окно боя с готовыми статблоками. " +
            "Вызывай ВМЕСТО придумывания ХП/КБ/атак вручную всякий раз, когда начинается бой. Ты задаёшь, КТО враги (имя, роль, архетип, тип), " +
            "харнес считает, НАСКОЛЬКО они сильны: подбирает уровень под сложность, симулирует бой против реальной группы (с её ХП, КБ и оружием) " +
            "и возвращает прогноз (раунды, потери ХП, риск), опыт за победу и статблоки. Если врагу задан level (из Bestiary.md) — уровень не меняется, " +
            "а прогноз честно покажет, что бой лёгкий или смертельный. Сложность: trivial / low / moderate (по умолчанию) / severe / extreme. " +
            "Роли: minion (прислужник, падает с 1–2 ударов), ordinary, elite (≈2 рядовых), quest_boss (≈4), dungeon_boss (≈6), arc_boss (≈8). " +
            "Архетипы: ",
            "Plan a fight with the fair balance formula and (by default) bring the adversaries into the battle window right away with ready stat blocks. " +
            "Call it INSTEAD of inventing HP/AC/attacks manually every time a fight starts. You set WHO the enemies are (name, role, archetype, type), " +
            "the harness calculates HOW strong they are: it picks the level for the difficulty, simulates the fight against the real party (with its HP, AC and weapons) " +
            "and returns a forecast (rounds, HP loss, risk), XP for victory and the stat blocks. If an enemy has a level (from Bestiary.md), the level does not change, " +
            "and the forecast honestly shows that the fight is easy or deadly. Difficulty: trivial / low / moderate (default) / severe / extreme. " +
            "Roles: minion (falls after 1–2 hits), ordinary, elite (≈2 rank-and-file), quest_boss (≈4), dungeon_boss (≈6), arc_boss (≈8). " +
            "Archetypes: ") + string.Join("; ", Progression.MonsterArchetypes.Select(a => $"{a.Key} — {a.Title}")) + ".",
            new JsonObject
            {
                ["enemies"] = new JsonObject
                {
                    ["type"] = "array",
                    ["description"] = L("Состав врагов", "The enemy roster"),
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["name"] = Prop("string", L("Имя; для нескольких одинаковых харнес пронумерует сам («Гоблин 1», «Гоблин 2»)", "Name; for several identical ones the harness numbers them itself (\"Goblin 1\", \"Goblin 2\")")),
                            ["count"] = Prop("integer", L("Сколько таких (1–12, по умолчанию 1)", "How many of them (1–12, default 1)")),
                            ["role"] = Prop("string", "minion / ordinary / elite / quest_boss / dungeon_boss / arc_boss"),
                            ["archetype"] = Prop("string", "standard / brute / skirmisher / defender / caster / sniper"),
                            ["level"] = Prop("integer", L("Фиксированный уровень существа (из Bestiary.md); не указывай — харнес подберёт под сложность", "A fixed creature level (from Bestiary.md); omit it — the harness picks one for the difficulty")),
                            ["portrait"] = Prop("string", L("id портрета из каталога (см. update_adversary); не указан — подберётся по имени", "A portrait id from the catalog (see update_adversary); if not given, picked by name")),
                            ["kind"] = Prop("string", L("Тип существа: humanoid, beast, undead, construct, dragon, demon, aberration, elemental, plant, ooze, giant, celestial, monstrosity (по умолчанию — по портрету)", "Creature type: humanoid, beast, undead, construct, dragon, demon, aberration, elemental, plant, ooze, giant, celestial, monstrosity (default — by portrait)")),
                            ["damage_type"] = Prop("string", L("Тип урона атаки: рубящий, колющий, огонь, яд…", "The attack's damage type: slashing, piercing, fire, poison…")),
                            ["abilities"] = Prop("string", L("Особые способности и тактика (не числа — числа даст харнес)", "Special abilities and tactics (not numbers — the harness provides the numbers)")),
                            ["notes"] = Prop("string", L("Слабости, сопротивления, добыча, поведение", "Weaknesses, resistances, loot, behavior")),
                        },
                        ["required"] = new JsonArray("name"),
                    },
                },
                ["difficulty"] = Prop("string", L("trivial / low / moderate / severe / extreme — желаемая сложность (по умолчанию moderate)", "trivial / low / moderate / severe / extreme — the desired difficulty (default moderate)")),
                ["spawn"] = Prop("boolean", L("Сразу вывести противников в бой (по умолчанию true). false — только прогноз", "Bring the adversaries into the fight right away (default true). false — forecast only")),
                ["replace"] = Prop("boolean", L("Убрать прежних противников из списка перед добавлением новых", "Remove the previous adversaries from the list before adding new ones")),
            },
            "enemies");

        yield return Def("award_xp",
            L("Начислить опыт герою. После победы (или если враги сдались/бежали) вызови с encounter=true — харнес посчитает опыт за всех противников " +
            "текущего боя по их силе относительно группы (как в PF2e: средний бой ≈ 80, тяжёлый ≈ 120). За задания — quest (minor 20, standard 60, " +
            $"major 120, arc_beat 150, arc 300). Уровень — каждые {Progression.XpPerLevel} опыта; когда порог достигнут, вызови level_up.",
            "Award XP to the hero. After a victory (or if the enemies surrendered/fled) call it with encounter=true — the harness calculates XP for all adversaries " +
            "of the current fight by their strength relative to the party (as in PF2e: an average fight ≈ 80, a hard one ≈ 120). For quests — quest (minor 20, standard 60, " +
            $"major 120, arc_beat 150, arc 300). A level every {Progression.XpPerLevel} XP; when the threshold is reached, call level_up."),
            ("encounter", "boolean", L("Опыт за противников текущего боя (каждый засчитывается один раз)", "XP for the adversaries of the current fight (each counted once)"), false),
            ("quest", "string", L("Опыт за задание: minor / standard / major / arc_beat / arc", "XP for a quest: minor / standard / major / arc_beat / arc"), false),
            ("amount", "integer", L("Произвольный опыт за находчивость или открытие (обычно 10–40)", "Arbitrary XP for resourcefulness or a discovery (usually 10–40)"), false),
            ("reason", "string", L("За что", "What for"), false));

        yield return Def("level_up",
            L("Повысить уровень героя или спутника по правилам прогрессии: харнес сам добавит ХП и ману по архетипу класса и сообщит, что растёт " +
            "автоматически (мастерство, кость мощи, защита) и что нужно выбрать (новый навык или ступень навыка, характеристики на 4/8/12/16/19 ур.). " +
            "Героя — только когда опыта достаточно; спутники растут вместе с героем и не могут его обогнать.",
            "Raise the level of the hero or a companion by the progression rules: the harness adds HP and mana by the class archetype itself and reports what grows " +
            "automatically (proficiency, the power die, defense) and what has to be chosen (a new skill or a skill tier, ability scores at levels 4/8/12/16/19). " +
            "The hero — only when there is enough XP; companions grow together with the hero and cannot overtake them."),
            ("owner", "string", L("hero или точное имя спутника", "hero or the exact name of a companion"), true),
            ("force", "boolean", L("Сюжетное повышение без опыта (милость богов, ритуал) — редко", "A story level-up without XP (divine favor, a ritual) — rare"), false));

        yield return Def("roll_loot",
            L("Честно разыграть добычу. После победы (source=battle) харнес бросает добычу с каждого поверженного противника по его уровню, роли и типу: " +
            "звери дают трофеи (клыки, шкуры, мясо), гуманоиды — мелочь из карманов, трофейное оружие, зелья и золото, драконы — чешую и клад, нежить — кости и старые реликвии. " +
            "Магические вещи выпадают не каждый бой: харнес копит удачу и выдаёт их честно в среднем, часто под снаряжение группы. " +
            "Предметы уже с характеристиками и уровнем кладутся в окно обыска с пометкой «↑ кому подойдёт» или «на продажу»; золото приходит числом — начисли его update_character после обыска. " +
            "Для сундука или тайника — source=chest (tier: chest — обычный тайник, hoard — сокровищница) и destination=bag, если окна боя нет. " +
            "Не выдумывай добычу поверх результата, кроме сюжетных предметов.",
            "Fairly roll the loot. After a victory (source=battle) the harness rolls loot from every defeated adversary by its level, role and type: " +
            "beasts give trophies (fangs, hides, meat), humanoids — pocket change, captured weapons, potions and gold, dragons — scales and hoards, undead — bones and old relics. " +
            "Magic items do not drop every fight: the harness accumulates luck and hands them out fairly on average, often matching the party's gear. " +
            "Items already with stats and a level go into the search window marked \"↑ who it suits\" or \"for sale\"; gold comes as a number — add it with update_character after the search. " +
            "For a chest or a cache — source=chest (tier: chest — an ordinary cache, hoard — a treasury) and destination=bag if there is no battle window. " +
            "Do not invent loot on top of the result, except story items."),
            ("source", "string", L("battle (по умолчанию, если есть противники) / chest", "battle (default if there are adversaries) / chest"), false),
            ("tier", "string", L("Для chest: chest или hoard", "For chest: chest or hoard"), false),
            ("level", "integer", L("Для chest: уровень добычи (по умолчанию уровень героя)", "For chest: the loot level (default — the hero's level)"), false),
            ("luck", "integer", L("Удача 0–3: находчивость, благословение, обыск с Внимательностью", "Luck 0–3: resourcefulness, a blessing, a search with Perception"), false),
            ("destination", "string", L("battle — окно обыска (по умолчанию), bag — сразу в сумку героя", "battle — the search window (default), bag — straight into the hero's bag"), false));

        yield return Def("update_companion_candidate",
            L("Вести естественный путь NPC к возможному вступлению в группу. Сначала создай этого человека через update_npc; затем action=introduce с мотивом, " +
            "личной целью, условием вступления и видимой игроку зацепкой. После реально сыгранной совместной сцены вызови shared_trial с её итогом. " +
            "Когда условие выполнено и в диалоге прозвучало взаимное согласие — action=ready. Отказ игрока или NPC фиксируй refused, окончательную потерю возможности — unavailable. " +
            "Кандидат не участвует в бою как член группы, пока recruit_companion не завершит найм.",
            "Track an NPC's natural path to possibly joining the party. First create this person via update_npc; then action=introduce with a motive, " +
            "a personal goal, a joining condition and a hook visible to the player. After a shared scene that was really played out call shared_trial with its outcome. " +
            "When the condition is met and mutual consent was voiced in dialogue — action=ready. Record a refusal by the player or the NPC as refused, a permanent loss of the opportunity as unavailable. " +
            "The candidate does not fight as a party member until recruit_companion completes the hiring."),
            ("name", "string", L("Точное имя уже созданного NPC", "The exact name of an already created NPC"), true),
            ("action", "string", "introduce / update / shared_trial / ready / refused / unavailable", true),
            ("source", "string", "arc / guild / world / hireling / history", false),
            ("arc", "string", L("Связанная сюжетная арка; если не указана, берётся активная", "The related story arc; if not given, the active one is used"), false),
            ("race", "string", L("Раса / происхождение", "Race / origin"), false),
            ("gender", "string", "Пол", false),
            ("portrait", "string", L("id облика NPC", "The NPC's look id"), false),
            ("concept", "string", L("Кто он и чем полезен истории — одна фраза", "Who they are and how they serve the story — one sentence"), false),
            ("motivation", "string", L("Собственный мотив, не сводящийся к помощи герою", "Their own motive, not just helping the hero"), false),
            ("personal_goal", "string", L("Личная цель или будущая линия спутника", "A personal goal or the companion's future storyline"), false),
            ("join_condition", "string", L("Что должно случиться до честного предложения вступить", "What must happen before an honest offer to join"), false),
            ("player_hint", "string", L("Короткая видимая зацепка без спойлеров", "A short visible hook without spoilers"), false),
            ("evidence", "string", L("Для shared_trial/ready/refused/unavailable: что реально произошло в сцене", "For shared_trial/ready/refused/unavailable: what really happened in the scene"), false));

        yield return Def("ask_companion_role",
            L("Спросить игрока, какую роль в группе займёт кандидат, который прошёл совместное испытание и явно согласился присоединиться (stage=ready). Игрок увидит карточки ролей " +
            "(урон в ближнем бою, урон издалека, магия, защита, лечение, поддержка, контроль). После вызова ход заканчивается; " +
            "выбор придёт сообщением, затем вызови recruit_companion с выбранной ролью.",
            "Ask the player which party role a candidate will take who passed a shared trial and explicitly agreed to join (stage=ready). The player sees role cards " +
            "(melee damage, ranged damage, magic, defense, healing, support, control). After the call the turn ends; " +
            "the choice arrives as a message, then call recruit_companion with the chosen role."),
            ("name", "string", L("Имя будущего спутника", "The future companion's name"), true),
            ("race", "string", "Раса", false),
            ("gender", "string", "Пол", false),
            ("concept", "string", L("Кто он в истории — одна фраза", "Who they are in the story — one sentence"), false));

        yield return RawDef(
            "recruit_companion",
            L("Добавить готового кандидата в группу после показанного игроку выбора роли. Харнес проверит весь путь знакомства и сам создаст сбалансированный лист на текущем уровне героя: " +
            "характеристики по роли, ХП и ману, снаряжение по уровню и 2–6 навыков с правильными ступенями и стоимостью маны. " +
            "Ты задаёшь класс (название под мир и роль: «храмовник», «пиромантка»), личность и, при желании, свои навыки — харнес выставит им ступень и ману по бюджету.",
            "Add a ready candidate to the party after the role choice was shown to the player. The harness checks the whole acquaintance path and builds a balanced sheet at the hero's current level itself: " +
            "ability scores by role, HP and mana, gear by level and 2–6 skills with the right tiers and mana costs. " +
            "You set the class (a name fitting the world and the role: \"templar\", \"pyromancer\"), the personality and, optionally, your own skills — the harness sets their tier and mana by the budget."),
            new JsonObject
            {
                ["name"] = Prop("string", L("Имя", "Name")),
                ["role"] = Prop("string", L("Выбранная роль: ", "The chosen role: ") + string.Join(", ", Progression.Archetypes.Select(a => $"{a.Key} ({a.Title.ToLowerInvariant()})"))),
                ["class"] = Prop("string", L("Название класса под мир и роль", "A class name fitting the world and the role")),
                ["race"] = Prop("string", L("Раса", "Race")),
                ["gender"] = Prop("string", L("Пол", "Gender")),
                ["level"] = Prop("integer", L("Уровень (по умолчанию — как у героя; выше героя нельзя)", "Level (default — the hero's; not above the hero)")),
                ["portrait"] = Prop("string", L("id облика (см. update_party_member); не указан — подберётся по расе, полу и классу", "A look id (see update_party_member); if not given, picked by race, gender and class")),
                ["personality"] = Prop("string", L("Характер, мотивы, отношение к герою — определяет, как спутник ведёт себя в бою", "Personality, motives, attitude to the hero — defines how the companion behaves in combat")),
                ["skills"] = new JsonObject
                {
                    ["type"] = "array",
                    ["description"] = L("Свои навыки вместо шаблонных (необязательно): название, категория, цель и эффект в костях мощи К", "Your own skills instead of the template ones (optional): name, category, target and effect in power dice K"),
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["name"] = Prop("string", L("Название", "Name")),
                            ["category"] = Prop("string", "attack / support / heal / cleanse / shield / revive / debuff / utility"),
                            ["target"] = Prop("string", "self / ally / enemy / all-allies / all-enemies"),
                            ["description"] = Prop("string", L("Эффект; урон и лечение — в костях мощи: «1.5К огнём + поджог»", "Effect; damage and healing — in power dice: \"1.5K fire + burning\"")),
                            ["icon"] = Prop("string", L("id иконки (см. update_skill)", "An icon id (see update_skill)")),
                        },
                        ["required"] = new JsonArray("name"),
                    },
                },
            },
            "name", "role");
    }

    // ===== Устойчивые к формату аргументы (модель присылает и числа, и строки) =====

    private static int? Num(JsonObject o, string key)
    {
        if (o[key] is not JsonValue v) return null;
        if (v.TryGetValue<int>(out var i)) return i;
        if (v.TryGetValue<double>(out var d)) return (int)Math.Round(d);
        return v.TryGetValue<string>(out var s) && int.TryParse(s.Trim().TrimStart('+'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : null;
    }

    private static string? Str(JsonObject o, string key) => o[key] switch
    {
        null => null,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        var n => n.ToString(),
    };

    private static bool Flag(JsonObject o, string key, bool fallback = false)
    {
        if (o[key] is not JsonValue v) return fallback;
        if (v.TryGetValue<bool>(out var b)) return b;
        return v.TryGetValue<string>(out var s) ? s.Trim().ToLowerInvariant() is "true" or "1" or "да" or "yes" : fallback;
    }

    // ===== Группа как бойцы симуляции =====

    private static int PartyLevel(RpgState state) => Progression.ParseLevel(state.Character.Level);

    private static Progression.Fighter FighterOf(ICombatant c, int hp)
    {
        var atk = ItemStats.BestAttack(c);
        return new Progression.Fighter(c.Name, Math.Max(1, hp), ItemStats.ArmorClass(c), atk.Attack, 1, atk.Dice, atk.Die, atk.Bonus, 1.25);
    }

    /// <summary>Герой и спутники. current — с текущими ХП и щитом (прогноз «прямо сейчас»), иначе с полными.</summary>
    private static List<Progression.Fighter> PartyFighters(RpgState state, bool current)
    {
        var list = new List<Progression.Fighter>();
        var c = state.Character;
        list.Add(FighterOf(c, current ? c.HpCurrent + c.Shield : Math.Max(c.HpMax, 1)));
        foreach (var m in state.Party)
        {
            if (current && m.HpCurrent <= 0) continue;
            list.Add(FighterOf(m, current ? m.HpCurrent + m.Shield : Math.Max(m.HpMax, 1)));
        }

        return list;
    }

    private static Progression.StatBlock BlockOf(Adversary a)
    {
        var level = Progression.ParseLevel(a.Level);
        var std = Progression.Monster(level, a.ThreatTier, a.Archetype);
        if (a.Ac <= 0 || a.HpMax <= 0)
        {
            return a.HpMax > 0 ? std with { Hp = a.HpMax } : std;
        }

        var (count, sides, _) = ItemStats.ParseDamage(a.Damage);
        var damage = a.Damage ?? "";
        var bonusMatch = System.Text.RegularExpressions.Regex.Match(damage, @"d\d+\s*([+\-−])\s*(\d+)");
        var bonus = bonusMatch.Success ? int.Parse(bonusMatch.Groups[2].Value) * (bonusMatch.Groups[1].Value == "+" ? 1 : -1) : 0;
        return std with
        {
            Hp = a.HpMax, Ac = a.Ac, Attack = a.AttackBonus, Attacks = Math.Max(1, a.Attacks),
            Dice = damage.Length > 0 ? count : std.Dice, Die = damage.Length > 0 ? sides : std.Die,
            DamageBonus = damage.Length > 0 ? bonus : std.DamageBonus,
        };
    }

    private static string DefaultDamageType(string kind, string archetype) => DamageWords.Localize(DefaultDamageTypeRu(kind, archetype));

    private static string DefaultDamageTypeRu(string kind, string archetype) => Genre.IsModern ? archetype switch
    {
        "caster" => "электричество",
        _ => kind switch
        {
            "beast" => "колющий",
            "vehicle" or "drone" => "огнестрельный",
            _ => archetype == "brute" ? "ударный" : "огнестрельный",
        },
    } : Genre.IsCyber ? archetype switch
    {
        "caster" => "кибератака",
        _ => kind switch
        {
            "program" => "кибератака",
            "mutant" or "beast" => "режущий",
            "mech" or "drone" => "кинетический",
            "cyborg" => archetype == "brute" ? "ударный" : "режущий",
            _ => archetype == "brute" ? "ударный" : "кинетический",
        },
    } : archetype switch
    {
        "caster" => "магический",
        "sniper" => "колющий",
        _ => kind switch
        {
            "beast" or "monstrosity" or "dragon" => "колющий",
            "ooze" => "кислотой",
            "elemental" => "стихией",
            "construct" or "giant" => "дробящий",
            "aberration" => "психический",
            "demon" => "огнём",
            _ => "рубящий",
        },
    };

    /// <summary>Записывает статблок в противника (уровень, ХП, КБ, атака, урон, стойкость).</summary>
    private static void ApplyBlock(Adversary a, Progression.StatBlock b, string damageType)
    {
        a.Level = b.Level.ToString(CultureInfo.InvariantCulture);
        a.HpMax = b.Hp;
        a.HpCurrent = b.Hp;
        a.Ac = b.Ac;
        a.AttackBonus = b.Attack;
        a.Attacks = b.Attacks;
        a.Damage = (b.DamageText + " " + damageType).Trim();
        a.SaveDc = b.SaveDc;
        a.StaggerMax = b.Stagger;
        a.Stagger = b.Stagger;
        a.Legendary = b.Legendary;
        a.Archetype = b.Archetype;
        a.ThreatTier = b.Role;
    }

    private static string BlockText(Adversary a) =>
        L($"ур.{a.Level}, ХП {a.HpMax}, КБ {a.Ac}, {(a.Attacks > 1 ? $"{a.Attacks} атаки" : "атака")} {a.AttackBonus:+#;-#;+0} урон {a.Damage}, DC {a.SaveDc}",
          $"lvl {a.Level}, HP {a.HpMax}, AC {a.Ac}, {(a.Attacks > 1 ? $"{a.Attacks} attacks" : "attack")} {a.AttackBonus:+#;-#;+0} damage {a.Damage}, DC {a.SaveDc}") +
        (a.StaggerMax > 0 ? L($", стойкость {a.StaggerMax}", $", poise {a.StaggerMax}") : "") + (a.Legendary > 0 ? L($", легендарных действий {a.Legendary}", $", legendary actions {a.Legendary}") : "");

    private static string HitChanceText(int attack, int ac) =>
        $"{Math.Round(Math.Clamp((21.0 - (ac - attack)) / 20.0, 0.05, 0.95) * 100)}%";

    // ===== plan_encounter =====

    private sealed record EnemySpec(string Name, int Count, string Role, string Archetype, int? Level, string? Portrait,
        string? Kind, string? DamageType, string? Abilities, string? Notes);

    private (bool Ok, string Result) PlanEncounter(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var specs = new List<EnemySpec>();
        foreach (var node in args["enemies"] as JsonArray ?? new JsonArray())
        {
            if (node is JsonObject o && (Str(o, "name") ?? "").Trim() is { Length: > 0 } name)
            {
                specs.Add(new EnemySpec(name, Math.Clamp(Num(o, "count") ?? 1, 1, 12), Progression.NormalizeRole(Str(o, "role")),
                    Progression.NormalizeMonsterArchetype(Str(o, "archetype")), Num(o, "level") is { } l && l > 0 ? Math.Clamp(l, 1, 60) : null,
                    Str(o, "portrait"), Str(o, "kind"), Str(o, "damage_type"), Str(o, "abilities"), Str(o, "notes")));
            }
            else if (node is JsonValue v && v.TryGetValue<string>(out var plain) && plain.Trim().Length > 0)
            {
                specs.Add(new EnemySpec(plain.Trim(), 1, "ordinary", "standard", null, null, null, null, null, null));
            }
        }

        if (specs.Count == 0) return (false, L("Ошибка: передай enemies — массив объектов {name, count, role, archetype, level?}.", "Error: pass enemies — an array of objects {name, count, role, archetype, level?}."));
        if (specs.Sum(s => s.Count) > 16) return (false, L("Ошибка: больше 16 противников в одном бою — раздели на волны.", "Error: more than 16 adversaries in one fight — split them into waves."));

        var state = _rpg.GetOrCreate(chatId);
        var partyLevel = PartyLevel(state);
        var party = PartyFighters(state, current: true);
        var partyFull = PartyFighters(state, current: false);
        var threat = Progression.NormalizeThreat(Str(args, "difficulty"));
        var center = Progression.Threats.First(t => t.Key == threat).Center;

        List<(EnemySpec Spec, Progression.StatBlock Block)> Build(int offset) =>
            specs.SelectMany(s => Enumerable.Range(0, s.Count).Select(_ =>
                (s, Progression.Monster(s.Level ?? Math.Clamp(partyLevel + offset, -2, 60), s.Role, s.Archetype)))).ToList();

        // Подбор уровня под сложность (против полной группы, чтобы раненый герой не получал ослабленных врагов).
        var offset = 0;
        var autoLevel = specs.Any(s => s.Level is null);
        if (autoLevel)
        {
            var best = double.MaxValue;
            for (var d = -8; d <= 6; d++)
            {
                var o = Progression.Simulate(partyFull, Build(d).Select(x => Progression.FromBlock(x.Spec.Name, x.Block)).ToList(), 160);
                var score = Math.Abs(o.Loss - center) + (o.WipeChance > 0.2 && threat != "extreme" ? 1 : 0) + Math.Abs(d) * 0.004;
                if (score < best)
                {
                    best = score;
                    offset = d;
                }
            }
        }

        var roster = Build(offset);
        var fighters = roster.Select(x => Progression.FromBlock(x.Spec.Name, x.Block)).ToList();
        var now = Progression.Simulate(party, fighters, 500);
        var full = Progression.Simulate(partyFull, fighters, 500);
        var xp = Progression.EncounterXp(partyLevel, partyFull.Count, fighters);

        var spawn = Flag(args, "spawn", true);
        // Живых противников не было — это новый бой: очередь хода прошлого боя только запутает.
        if (spawn && !state.InCombat() && state.Initiative.Count > 0)
        {
            state.Initiative.Clear();
        }
        if (spawn && !state.InCombat())
        {
            // Новый бой: прошлые бежавшие и сдавшиеся уже не на поле — их запись мешала бы очереди и морали.
            state.Adversaries.RemoveAll(a => !a.InFight && a.XpGiven && (a.LootRolled || a.Morale.Length > 0));
            state.CombatRound = 0;
            state.CombatCurrentActor = "";
        }

        var lines = new List<string>();
        var portraitNotes = new List<string>();
        if (spawn && Flag(args, "replace"))
        {
            state.Adversaries.Clear();
        }

        var counters = new Dictionary<EnemySpec, int>();
        foreach (var (spec, block) in roster)
        {
            counters[spec] = counters.GetValueOrDefault(spec) + 1;
            var name = spec.Count > 1 && !System.Text.RegularExpressions.Regex.IsMatch(spec.Name, @"\d+\s*$")
                ? $"{spec.Name} {counters[spec]}"
                : spec.Name;
            var (portrait, note) = PortraitCatalog.ResolveEnemy(spec.Portrait, spec.Name, block.Role);
            if (note.Length > 0 && counters[spec] == 1) portraitNotes.Add(note);
            var kind = (spec.Kind ?? portrait?.Kind ?? "humanoid").Trim().ToLowerInvariant();
            var dmgType = (spec.DamageType ?? DefaultDamageType(kind, block.Archetype)).Trim();

            var adv = new Adversary { Name = name };
            if (spawn)
            {
                adv = state.Adversaries.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? adv;
                if (!state.Adversaries.Contains(adv)) state.Adversaries.Add(adv);
                adv.Effects.Clear();
                adv.Status = "";
                adv.XpGiven = false;
                adv.LootRolled = false;
                adv.Morale = "";
                adv.MoraleTests.Clear();
            }

            ApplyBlock(adv, block, dmgType);
            adv.Portrait = portrait?.Id ?? "";
            adv.Kind = kind;
            adv.Abilities = (spec.Abilities ?? "").Trim() is { Length: > 0 } ab
                ? $"{block.AttackText(dmgType)}. {ab}"
                : block.AttackText(dmgType) + (block.Role is "minion" ? L("; падает от 1–2 ударов", "; falls after 1–2 hits") : "");
            if (spec.Notes is { Length: > 0 } nt) adv.Notes = nt.Trim();

            lines.Add($"- {name} [{Progression.RoleTitle(block.Role)}, {Progression.MonsterArchetypes.First(a => a.Key == block.Archetype).Title.Split(':')[0]}, " +
                      $"{PortraitCatalog.KindTitle(kind)}] {BlockText(adv)}; {L("портрет", "portrait")} {adv.Portrait}");
        }

        if (spawn) _rpg.Save(chatId, state);

        var sb = new StringBuilder();
        sb.Append(spawn ? L("OK: противники выведены в бой. ", "OK: the adversaries are brought into the fight. ") : L("OK: прогноз (противники не добавлены). ", "OK: forecast (adversaries not added). "));
        sb.Append(L($"Сложность «{Progression.ThreatTitle(full.Threat)}» для группы ур. {partyLevel} из {partyFull.Count}: ≈{full.Rounds:0.#} раунда, " +
                  $"группа потеряет ~{full.Loss * 100:0}% ХП; кто-то упадёт с шансом {full.DownChance * 100:0}%, падут все — {full.WipeChance * 100:0}%.",
                  $"Difficulty \"{Progression.ThreatTitle(full.Threat)}\" for a party of level {partyLevel} of {partyFull.Count}: ≈{full.Rounds:0.#} rounds, " +
                  $"the party will lose ~{full.Loss * 100:0}% HP; someone falls with a chance of {full.DownChance * 100:0}%, everyone falls — {full.WipeChance * 100:0}%."));
        if (Math.Abs(now.Loss - full.Loss) > 0.08 || now.WipeChance - full.WipeChance > 0.05)
        {
            sb.Append(L($" С ТЕКУЩИМИ ранами группы бой «{Progression.ThreatTitle(now.Threat)}»: потери ~{now.Loss * 100:0}%, риск падения всех {now.WipeChance * 100:0}%.", $" With the party's CURRENT wounds the fight is \"{Progression.ThreatTitle(now.Threat)}\": losses ~{now.Loss * 100:0}%, risk of everyone falling {now.WipeChance * 100:0}%."));
        }

        if (full.Threat != threat)
        {
            sb.Append(autoLevel
                ? L($" Заказана «{Progression.ThreatTitle(threat)}», ближе не подобрать этим составом — измени число или роли врагов.", $" \"{Progression.ThreatTitle(threat)}\" was ordered; this roster cannot get closer — change the number or roles of the enemies.")
                : L($" Заказана «{Progression.ThreatTitle(threat)}», но уровень врагов задан явно — это честная оценка их силы.", $" \"{Progression.ThreatTitle(threat)}\" was ordered, but the enemy level is set explicitly — this is an honest estimate of their strength."));
        }

        sb.Append(L($" Опыт за победу: {xp} (после боя — award_xp encounter=true).", $" XP for victory: {xp} (after the fight — award_xp encounter=true)."));
        sb.AppendLine();
        sb.AppendLine(autoLevel ? L("Уровень противников подобран: ", "Adversary level picked: ") + $"{Math.Clamp(partyLevel + offset, -2, 60)}{(partyLevel + offset < 1 ? L(" (ослабленные существа для начинающего героя)", " (weakened creatures for a beginner hero)") : "")}." : L("Уровни противников заданы.", "Adversary levels are set."));
        sb.AppendLine(string.Join("\n", lines));
        var heroAtk = ItemStats.BestAttack(state.Character);
        var sampleAc = roster[0].Block.Ac;
        sb.AppendLine(L($"Атаки врагов: 1d20+бонус против КБ цели (герой КБ {ItemStats.ArmorClass(state.Character)}", $"Enemy attacks: 1d20+bonus against the target's AC (hero AC {ItemStats.ArmorClass(state.Character)}") +
                      string.Concat(state.Party.Select(m => $", {m.Name} {L("КБ", "AC")} {ItemStats.ArmorClass(m)}")) + L("), урон — отдельным roll_dice. ", "), resolved by resolve_attack. ") +
                      L($"Герой ({heroAtk.Title}, {heroAtk.Attack:+#;-#;+0}) попадает по КБ {sampleAc} на {HitChanceText(heroAtk.Attack, sampleAc)}.", $"The hero ({heroAtk.Title}, {heroAtk.Attack:+#;-#;+0}) hits AC {sampleAc} {HitChanceText(heroAtk.Attack, sampleAc)} of the time."));
        if (portraitNotes.Count > 0) sb.AppendLine(L("Портреты: ", "Portraits: ") + string.Join("; ", portraitNotes) + ".");
        if (spawn) sb.AppendLine(L("Дальше: initiative roll_all=true (засада — ambush), затем действия через resolve_attack и combat_turn advance. Мораль врагов харнес проверяет сам: раненые и оставшиеся без вожака могут бежать или сдаться.", "Next: initiative roll_all=true (an ambush — ambush), then actions via resolve_attack and combat_turn advance. The harness checks enemy morale itself: the wounded and those left without a leader may flee or surrender."));
        sb.Append(L("Не меняй эти числа в повествовании; особые способности отыгрывай в рамках урона и DC статблока (resolve_attack с damage, save, effect).", "Do not change these numbers in the narration; play special abilities within the stat block's damage and DC (resolve_attack with damage, save, effect)."));
        return (true, sb.ToString());
    }

    // ===== award_xp =====

    private (bool Ok, string Result) AwardXp(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var state = _rpg.GetOrCreate(chatId);
        var c = state.Character;
        var level = PartyLevel(state);
        var parts = new List<string>();
        var total = 0;

        var forEncounter = Flag(args, "encounter");
        if (forEncounter)
        {
            var fighting = state.Adversaries.Count(a => !a.XpGiven && a.InFight);
            var foes = state.Adversaries.Where(a => !a.XpGiven && !a.InFight && (a.HpMax > 0 || a.Level.Length > 0)).ToList();
            if (foes.Count == 0)
            {
                return (false, fighting > 0
                    ? L($"Ошибка: бой ещё идёт — {fighting} противн. сражаются. Опыт дают за поверженных, бежавших и сдавшихся; если враги отступили по сюжету — отметь это update_adversary morale=fled.", $"Error: the fight is still on — {fighting} adversaries are fighting. XP is awarded for the defeated, the fled and the surrendered; if the enemies retreated by the story, mark it with update_adversary morale=fled.")
                    : L("Ошибка: нет противников, за которых опыт ещё не начислен. Не удаляй врагов до award_xp; за задания используй quest.", "Error: there are no adversaries without awarded XP. Do not remove enemies before award_xp; for quests use quest."));
            }

            if (fighting > 0) parts.Add(L($"ещё сражаются: {fighting} — опыт за них после боя", $"still fighting: {fighting} — XP for them after the fight"));

            var fighters = foes.Select(a => Progression.FromBlock(a.Name, BlockOf(a))).ToList();
            var xp = Progression.EncounterXp(level, 1 + state.Party.Count, fighters);
            foreach (var a in foes) a.XpGiven = true;
            total += xp;
            parts.Add(L($"бой ({foes.Count} противн.) +{xp}", $"fight ({foes.Count} adversaries) +{xp}"));
        }

        if (Str(args, "quest") is { Length: > 0 } quest)
        {
            var q = Progression.QuestXp(quest);
            total += q;
            parts.Add(L($"задание ({quest}) +{q}", $"quest ({quest}) +{q}"));
        }

        if (Num(args, "amount") is { } amount && amount > 0)
        {
            var a = Math.Min(amount, Progression.XpPerLevel / 2);
            total += a;
            parts.Add(L($"находчивость +{a}", $"resourcefulness +{a}"));
        }

        if (total == 0) return (false, L("Ошибка: передай encounter=true, quest или amount.", "Error: pass encounter=true, quest or amount."));

        var before = c.Xp;
        c.Xp += total;
        _rpg.Save(chatId, state);

        var next = Progression.XpFor(level + 1);
        var sb = new StringBuilder($"OK: {L("опыт", "XP")} {before} → {c.Xp} ({string.Join(", ", parts)}{(Str(args, "reason") is { Length: > 0 } r ? $"; {r.Trim()}" : "")}). ");
        if (c.Xp >= next)
        {
            var reachable = Progression.LevelForXp(c.Xp);
            sb.Append(L($"Опыта хватает на уровень {level + 1}{(reachable > level + 1 ? $" (и далее до {reachable})" : "")}: объяви это игроку и вызови level_up owner=hero, " +
                      "затем level_up для каждого спутника.",
                      $"Enough XP for level {level + 1}{(reachable > level + 1 ? $" (and further up to {reachable})" : "")}: announce it to the player and call level_up owner=hero, " +
                      "then level_up for each companion."));
        }
        else
        {
            sb.Append(L($"До уровня {level + 1}: {next - c.Xp} опыта.", $"To level {level + 1}: {next - c.Xp} XP."));
        }

        // Опыт за бой капает последним — прямо здесь напоминаем про выбор, иначе ход
        // часто заканчивается текстом и кнопки вариантов игрок не видит.
        if (forEncounter)
        {
            sb.Append(L(" Бой окончен: после итогов заверши ход через offer_choices — 2–4 варианта (обыск, лечение, путь дальше).", " The fight is over: after the summary finish the turn with offer_choices — 2–4 options (search, healing, the way onward)."));
        }

        return (true, sb.ToString());
    }

    // ===== level_up =====

    private (bool Ok, string Result) LevelUp(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var state = _rpg.GetOrCreate(chatId);
        var owner = (Str(args, "owner") ?? "hero").Trim();
        var force = Flag(args, "force");
        var heroLevel = PartyLevel(state);
        var isHero = owner.Equals("hero", StringComparison.OrdinalIgnoreCase) || owner.Equals(state.Character.Name, StringComparison.OrdinalIgnoreCase);
        ICombatant? who = isHero ? state.Character : state.Party.FirstOrDefault(p => p.Name.Equals(owner, StringComparison.OrdinalIgnoreCase));
        if (who is null) return (false, L($"Ошибка: «{owner}» нет среди героя и спутников.", $"Error: \"{owner}\" is neither the hero nor a companion."));

        var from = Progression.ParseLevel(who.Level);
        var to = from + 1;
        if (isHero && !force && state.Character.Xp < Progression.XpFor(to))
        {
            return (false, L($"Ошибка: для уровня {to} нужно {Progression.XpFor(to)} опыта, у героя {state.Character.Xp}. Начисли опыт через award_xp.", $"Error: level {to} needs {Progression.XpFor(to)} XP, the hero has {state.Character.Xp}. Award XP via award_xp."));
        }

        if (!isHero && !force && to > heroLevel)
        {
            return (false, L($"Ошибка: спутник не может обогнать героя (герой ур. {heroLevel}).", $"Error: a companion cannot overtake the hero (hero level {heroLevel})."));
        }

        var arch = Progression.ArchetypeOf(who);
        var conMod = DndStatNames.Modifier(who.Stat(DndStat.Con));
        var hpGain = isHero
            ? Progression.HeroHpPerLevel(arch, conMod)
            : Progression.CompanionHpPerLevel(arch, conMod);
        var manaGain = arch.ManaPerLevel;
        if (who is CharacterSheet h)
        {
            h.Level = to.ToString(CultureInfo.InvariantCulture);
            h.HpMax += hpGain;
            h.HpCurrent += hpGain;
            h.ManaMax += manaGain;
            h.ManaCurrent += manaGain;
        }
        else if (who is PartyMember m)
        {
            m.Level = to.ToString(CultureInfo.InvariantCulture);
            m.HpMax += hpGain;
            m.HpCurrent += hpGain;
            m.ManaMax += manaGain;
            m.ManaCurrent += manaGain;
        }

        _rpg.Save(chatId, state);

        var sb = new StringBuilder(L($"OK: {(isHero ? "герой" : who.Name)} — уровень {from} → {to} ({arch.Title.ToLowerInvariant()}). ХП +{hpGain}, мана +{manaGain}.",
                                     $"OK: {(isHero ? "the hero" : who.Name)} — level {from} → {to} ({arch.Title.ToLowerInvariant()}). HP +{hpGain}, mana +{manaGain}."));
        if (Progression.Proficiency(to) > Progression.Proficiency(from)) sb.Append(L(" Мастерство +", " Proficiency +") + Progression.Proficiency(to) + ".");
        if (Progression.PowerDice(to) > Progression.PowerDice(from)) sb.Append(L($" Кость мощи выросла: К = {Progression.PowerDice(to)}d8 — удары и навыки бьют сильнее.", $" The power die grew: K = {Progression.PowerDice(to)}d8 — strikes and skills hit harder."));
        if (Progression.DefenseBonus(to) > Progression.DefenseBonus(from)) sb.Append(L($" Боевой опыт: +{Progression.DefenseBonus(to)} к КБ.", $" Combat experience: +{Progression.DefenseBonus(to)} to AC."));
        if (Progression.MaxSkillRank(to) > Progression.MaxSkillRank(from)) sb.Append(L(" Открыта ступень навыков ", " Skill tier unlocked: ") + Progression.MaxSkillRank(to) + ".");
        var rankCap = Progression.MaxSkillRank(to);
        sb.Append(rankCap > 1
            ? L($" ВЫБОР: один навык — выучить новый (ступень до {rankCap}) или поднять ступень существующего (update_skill).", $" CHOICE: one skill — learn a new one (tier up to {rankCap}) or raise the tier of an existing one (update_skill).")
            : L(" ВЫБОР: выучить один новый навык ступени 1 (update_skill); повышать ступени можно с 5-го уровня.", " CHOICE: learn one new tier 1 skill (update_skill); tiers can be raised from level 5."));
        if (Progression.IsStatLevel(to)) sb.Append(L(" Также +2 к характеристикам (+2 к одной или +1 к двум, не выше 20).", " Also +2 to ability scores (+2 to one or +1 to two, not above 20)."));
        sb.Append(isHero
            ? L(" Выбор делает игрок: предложи 2–4 варианта через offer_choices. Спутники растут вместе с героем — вызови level_up для каждого.", " The player makes the choice: offer 2–4 options via offer_choices. Companions grow together with the hero — call level_up for each.")
            : L(" Выбор делай сам — по классу и характеру спутника — и коротко сообщи игроку.", " Make the choice yourself — by the companion's class and personality — and briefly tell the player."));
        return (true, sb.ToString());
    }

    // ===== roll_loot =====

    private (bool Ok, string Result) RollLoot(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var state = _rpg.GetOrCreate(chatId);
        var luck = Math.Clamp(Num(args, "luck") ?? 0, 0, 3);
        var source = (Str(args, "source") ?? (state.Adversaries.Count > 0 ? "battle" : "chest")).Trim().ToLowerInvariant();
        var destination = (Str(args, "destination") ?? (source == "battle" ? "battle" : state.Adversaries.Count > 0 ? "battle" : "bag")).Trim().ToLowerInvariant();
        var rng = Random.Shared;
        var sources = new List<LootTables.LootSource>();
        var from = new List<string>();

        if (source == "battle")
        {
            var fallen = state.Adversaries.Where(a => !a.LootRolled && (a.HpCurrent <= 0 || a.Morale == "surrendered")).ToList();
            if (fallen.Count == 0)
            {
                return (false, L("Ошибка: нет поверженных противников без разыгранной добычи (ХП 0). Бежавшие и сдавшиеся добычу не оставляют — их вещи решай по сюжету.", "Error: there are no defeated adversaries (HP 0) without rolled loot. The fled leave no loot — decide their belongings by the story."));
            }

            foreach (var a in fallen)
            {
                var kind = a.Kind.Length > 0 ? a.Kind : PortraitCatalog.ResolveEnemy(a.Portrait, a.Name, a.ThreatTier).Portrait?.Kind ?? "humanoid";
                var creature = System.Text.RegularExpressions.Regex.Replace(a.Name, @"\s*\d+\s*$", "").Trim().ToLowerInvariant();
                sources.Add(new LootTables.LootSource(Progression.ParseLevel(a.Level), a.ThreatTier, kind, creature));
                a.LootRolled = true;
                from.Add(a.Name);
            }
        }
        else
        {
            var tier = (Str(args, "tier") ?? "chest").Trim().ToLowerInvariant() == "hoard" ? "hoard" : "chest";
            var level = Math.Clamp(Num(args, "level") ?? PartyLevel(state), 1, 60);
            sources.Add(new LootTables.LootSource(level, tier, "humanoid", ""));
            from.Add(tier == "hoard" ? L("сокровищница", "treasury") : L("тайник", "cache"));
        }

        // Весь бой бросается разом: магические находки идут через копилку, а не по броску на каждого врага.
        var loot = LootTables.RollEncounter(_catalog, _economy, state, sources, rng, luck);
        var items = loot.Items;
        var gold = loot.Gold;

        // Одинаковые трофеи с нескольких врагов — одной стопкой.
        var merged = new List<GridItem>();
        foreach (var it in items)
        {
            var same = merged.FirstOrDefault(m => m.Name == it.Name && m.Icon == it.Icon && m.Rarity == it.Rarity && m.Slot is null && it.Slot is null);
            if (same is null) merged.Add(it); else same.Quantity += it.Quantity;
        }

        var notes = new List<string>();
        foreach (var it in merged.Where(i => i.Rarity == "legendary"))
        {
            if (state.LegendaryTokens > 0)
            {
                state.LegendaryTokens--;
                notes.Add(L($"«{it.Name}» — легендарная (израсходовано разрешение)", $"\"{it.Name}\" — legendary (a permission spent)"));
            }
            else
            {
                it.Rarity = "epic";
                ItemStats.Balance(it);
                notes.Add(L($"«{it.Name}» понижена до эпической — нет разрешения на легендарное", $"\"{it.Name}\" downgraded to epic — no legendary permission"));
            }
        }

        var lost = new List<string>();
        foreach (var it in merged)
        {
            if (destination == "bag")
            {
                var spot = InventoryOps.FindFree(state.Grid, it.W, it.H);
                if (spot is null)
                {
                    lost.Add(it.Name);
                    continue;
                }

                (it.Col, it.Row) = spot.Value;
                state.Grid.Add(it);
            }
            else
            {
                state.BattleLoot.Add(it);
            }
        }

        _rpg.Save(chatId, state);
        var list = merged.Where(i => !lost.Contains(i.Name)).Select(i =>
            $"{i.Name}{(i.Quantity > 1 ? $" x{i.Quantity}" : "")}{(i.Rarity != "common" ? $" [{ItemEconomy.RarityTitle(i.Rarity)}, {L("ур.", "lvl ")}{i.Level}]" : "")}" +
            (ItemStats.HasStats(i) ? $" {{{ItemStats.Describe(i)}}}" : "") + $" ~{_economy.UnitValue(i)} {L("з", Genre.Coin)}" +
            (LootAdvisor.For(_economy, state, i) is { Kind: not LootAdvisor.AdviceKind.None } adv ? $" ({adv.Text})" : "")).ToList();
        var upgrades = merged.Count(i => LootAdvisor.For(_economy, state, i).Kind == LootAdvisor.AdviceKind.Upgrade);
        var sb = new StringBuilder(L($"OK: добыча ({string.Join(", ", from)}) {(destination == "bag" ? "в сумке героя" : "в окне обыска")}: ",
                                     $"OK: loot ({string.Join(", ", from)}) {(destination == "bag" ? "in the hero's bag" : "in the search window")}: "));
        sb.Append(list.Count > 0 ? string.Join("; ", list) : L("ничего ценного", "nothing of value"));
        sb.Append('.');
        sb.Append(gold > 0 ? L($" Золото: {gold} — начисли update_character gold={state.Character.Gold + gold} {(destination == "bag" ? "сейчас" : "после обыска")}.",
                               $" Gold: {gold} — add it with update_character gold={state.Character.Gold + gold} {(destination == "bag" ? "now" : "after the search")}.") : L(" Золота нет.", " No gold."));
        if (notes.Count > 0) sb.Append(" " + string.Join("; ", notes) + ".");
        if (lost.Count > 0) sb.Append(L($" В сумке нет места для: {string.Join(", ", lost)} — предложи игроку что-то выбросить.", $" No room in the bag for: {string.Join(", ", lost)} — offer the player to throw something away."));
        if (upgrades > 0) sb.Append(L($" Улучшений для группы: {upgrades} (помечены ↑) — подчеркни их в рассказе; остальное — трофеи на продажу.", $" Upgrades for the party: {upgrades} (marked ↑) — highlight them in the story; the rest are trophies for sale."));
        sb.Append(L(" Опиши находки в духе мира; можешь дать вещам местные названия в повествовании, но числа не меняй. " +
                  "Затем заверши ход через offer_choices — 2–4 варианта, что делать дальше.",
                  " Describe the finds in the spirit of the world; you may give the items local names in the narration, but do not change the numbers. " +
                  "Then finish the turn with offer_choices — 2–4 options for what to do next."));
        return (true, sb.ToString());
    }

    // ===== Спутники =====

    private (bool Ok, string Result) UpdateCompanionCandidate(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var name = (Str(args, "name") ?? "").Trim();
        var action = (Str(args, "action") ?? "").Trim().ToLowerInvariant();
        if (name.Length == 0) return (false, L("Ошибка: нужно name.", "Error: name is required."));
        if (action is not ("introduce" or "update" or "shared_trial" or "ready" or "refused" or "unavailable"))
            return (false, L("Ошибка: action должен быть introduce, update, shared_trial, ready, refused или unavailable.", "Error: action must be introduce, update, shared_trial, ready, refused or unavailable."));

        var state = _rpg.GetOrCreate(chatId);
        if (state.Party.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return (false, L($"Ошибка: «{name}» уже в группе; меняй спутника через update_party_member.", $"Error: \"{name}\" is already in the party; change the companion via update_party_member."));

        var npc = state.ImportantCharacters.Concat(state.Notables)
            .FirstOrDefault(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var candidate = state.CompanionCandidates.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (action == "introduce")
        {
            if (npc is null)
                return (false, L($"Ошибка: «{name}» ещё не существует в мире. Сначала создай NPC через update_npc и сыграй знакомство, затем повтори introduce.", $"Error: \"{name}\" does not exist in the world yet. First create the NPC via update_npc and play out the meeting, then repeat introduce."));
            if (candidate is null && state.CompanionCandidates.Count(c => c.Stage is "introduced" or "proven" or "ready") >= 2)
                return (false, L("Ошибка: уже есть два открытых кандидата. Развей одну из этих линий или закрой её через refused/unavailable, чтобы не превращать историю в кастинг.", "Error: there are already two open candidates. Develop one of these lines or close it via refused/unavailable, so as not to turn the story into a casting call."));

            candidate ??= new CompanionCandidate { Name = name };
            var concept = (Str(args, "concept") ?? candidate.Concept).Trim();
            var motivation = (Str(args, "motivation") ?? candidate.Motivation).Trim();
            var personalGoal = (Str(args, "personal_goal") ?? candidate.PersonalGoal).Trim();
            var joinCondition = (Str(args, "join_condition") ?? candidate.JoinCondition).Trim();
            var playerHint = (Str(args, "player_hint") ?? candidate.PlayerHint).Trim();
            if (new[] { concept, motivation, personalGoal, joinCondition, playerHint }.Any(string.IsNullOrWhiteSpace))
                return (false, L("Ошибка: для introduce нужны concept, motivation, personal_goal, join_condition и player_hint. Кандидат должен иметь собственную причину жить в мире, а не возникать ради свободного слота группы.", "Error: introduce needs concept, motivation, personal_goal, join_condition and player_hint. A candidate must have their own reason to live in the world, not appear for a free party slot."));
            candidate.Concept = concept;
            candidate.Motivation = motivation;
            candidate.PersonalGoal = personalGoal;
            candidate.JoinCondition = joinCondition;
            candidate.PlayerHint = playerHint;
            candidate.Source = NormalizeCandidateSource(Str(args, "source") ?? candidate.Source);
            candidate.Arc = (Str(args, "arc") ?? state.StoryArcs.FirstOrDefault(a => a.State == "active")?.Name ?? candidate.Arc).Trim();
            candidate.Race = (Str(args, "race") ?? candidate.Race).Trim();
            candidate.Gender = (Str(args, "gender") ?? candidate.Gender).Trim();
            candidate.Portrait = (Str(args, "portrait") ?? candidate.Portrait).Trim();
            if (!state.CompanionCandidates.Contains(candidate)) state.CompanionCandidates.Add(candidate);
            candidate.Stage = candidate.SharedTrials > 0 ? "proven" : "introduced";
            _rpg.Save(chatId, state);
            return (true, L($"OK: «{name}» отмечен как возможный спутник [{candidate.Source}], этап «знакомство». Не предлагай вступление сразу: сначала сыграй значимую совместную сцену, затем вызови shared_trial.", $"OK: \"{name}\" is marked as a possible companion [{candidate.Source}], stage \"introduced\". Do not offer joining right away: first play a meaningful shared scene, then call shared_trial."));
        }

        if (candidate is null)
            return (false, L($"Ошибка: «{name}» не зарегистрирован как кандидат. Сначала update_npc, затем update_companion_candidate action=introduce.", $"Error: \"{name}\" is not registered as a candidate. First update_npc, then update_companion_candidate action=introduce."));
        if (candidate.Stage is "recruited" or "unavailable")
            return (false, L($"Ошибка: линия «{name}» уже закрыта ({candidate.Stage}).", $"Error: the line of \"{name}\" is already closed ({candidate.Stage})."));

        var evidence = (Str(args, "evidence") ?? "").Trim();
        if (action != "update" && evidence.Length < 12)
            return (false, L($"Ошибка: для action={action} нужно evidence — конкретный итог уже сыгранной сцены, а не план на будущее.", $"Error: action={action} needs evidence — a concrete outcome of an already played scene, not a plan for the future."));

        void SetIf(string key, Action<string> set)
        {
            if (Str(args, key) is { } value && !string.IsNullOrWhiteSpace(value)) set(value.Trim());
        }
        SetIf("concept", v => candidate.Concept = v);
        SetIf("motivation", v => candidate.Motivation = v);
        SetIf("personal_goal", v => candidate.PersonalGoal = v);
        SetIf("join_condition", v => candidate.JoinCondition = v);
        SetIf("player_hint", v => candidate.PlayerHint = v);
        SetIf("race", v => candidate.Race = v);
        SetIf("gender", v => candidate.Gender = v);
        SetIf("portrait", v => candidate.Portrait = v);
        SetIf("arc", v => candidate.Arc = v);
        SetIf("source", v => candidate.Source = NormalizeCandidateSource(v));

        if (action == "update")
        {
            _rpg.Save(chatId, state);
            return (true, L($"OK: линия кандидата «{name}» обновлена; этап остаётся «{candidate.Stage}».", $"OK: the candidate line of \"{name}\" is updated; the stage stays \"{candidate.Stage}\"."));
        }

        if (action == "shared_trial")
        {
            if (candidate.Stage is not ("introduced" or "proven" or "refused"))
                return (false, L($"Ошибка: совместное испытание нельзя записать на этапе «{candidate.Stage}».", $"Error: a shared trial cannot be recorded at the \"{candidate.Stage}\" stage."));
            candidate.SharedTrials++;
            candidate.Stage = "proven";
            candidate.RoleOffered = false;
            candidate.Milestones.Add(evidence);
            if (candidate.Milestones.Count > 8) candidate.Milestones.RemoveAt(0);
            _rpg.Save(chatId, state);
            return (true, L($"OK: совместное испытание «{name}» записано ({candidate.SharedTrials}). Теперь выполни условие вступления и проведи явный разговор; только после взаимного согласия вызывай action=ready.", $"OK: the shared trial with \"{name}\" is recorded ({candidate.SharedTrials}). Now fulfill the joining condition and hold an explicit conversation; call action=ready only after mutual consent."));
        }

        if (action == "ready")
        {
            if (candidate.SharedTrials < 1)
                return (false, L("Ошибка: кандидат ещё не прошёл с героем ни одного значимого совместного испытания. Сначала сыграй сцену и вызови shared_trial.", "Error: the candidate has not gone through a single meaningful shared trial with the hero yet. First play a scene and call shared_trial."));
            candidate.Milestones.Add(evidence);
            if (candidate.Milestones.Count > 8) candidate.Milestones.RemoveAt(0);
            candidate.Stage = "ready";
            candidate.RoleOffered = false;
            _rpg.Save(chatId, state);
            return (true, L($"OK: «{name}» готов присоединиться после взаимного согласия. Теперь вызови ask_companion_role; игрок выберет не личность, а боевую роль.", $"OK: \"{name}\" is ready to join after mutual consent. Now call ask_companion_role; the player chooses not the personality but the combat role."));
        }

        candidate.Milestones.Add(evidence);
        if (candidate.Milestones.Count > 8) candidate.Milestones.RemoveAt(0);
        candidate.Stage = action;
        candidate.RoleOffered = false;
        _rpg.Save(chatId, state);
        return (true, action == "refused"
            ? L($"OK: предложение «{name}» отклонено и запомнено. Не повторяй его без нового весомого сюжетного повода.", $"OK: the offer with \"{name}\" is refused and remembered. Do not repeat it without a new weighty story reason.")
            : L($"OK: возможность нанять «{name}» окончательно закрыта; сохрани последствия в повествовании.", $"OK: the opportunity to hire \"{name}\" is closed for good; keep the consequences in the narration."));
    }

    private static string NormalizeCandidateSource(string source) => source.Trim().ToLowerInvariant() switch
    {
        "arc" or "guild" or "world" or "hireling" or "history" => source.Trim().ToLowerInvariant(),
        _ => "world",
    };

    private (bool Ok, string Result) AskCompanionRole(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var name = (Str(args, "name") ?? "").Trim();
        if (name.Length == 0) return (false, L("Ошибка: нужно name.", "Error: name is required."));
        var state = _rpg.GetOrCreate(chatId);
        if (state.Party.Count >= 3) return (false, L("Ошибка: в группе уже три спутника — это максимум.", "Error: the party already has three companions — that is the maximum."));
        var candidate = state.CompanionCandidates.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (candidate is null)
            return (false, L($"Ошибка: «{name}» не зарегистрирован как кандидат. Пройди update_npc → update_companion_candidate introduce → shared_trial → ready.", $"Error: \"{name}\" is not registered as a candidate. Go through update_npc → update_companion_candidate introduce → shared_trial → ready."));
        if (candidate.Stage != "ready")
            return (false, L($"Ошибка: «{name}» пока на этапе «{candidate.Stage}». Роли можно показать только после совместного испытания и явного согласия (stage=ready).", $"Error: \"{name}\" is still at the \"{candidate.Stage}\" stage. Roles can be shown only after a shared trial and explicit consent (stage=ready)."));
        candidate.RoleOffered = true;
        _rpg.Save(chatId, state);
        return (true, L($"OK: игроку показаны роли для «{name}». Заверши ход и дождись выбора; затем вызови recruit_companion с выбранной ролью.", $"OK: the roles for \"{name}\" are shown to the player. End the turn and wait for the choice; then call recruit_companion with the chosen role."));
    }

    /// <summary>Шаблонные навыки ролей. {K} — урон/лечение в костях мощи по ступени, {R} — ступень.</summary>
    private static Dictionary<string, (string Name, string Cat, string Target, string Desc)[]> RoleSkills => Lang.IsEn
        ? Genre.Pick(FantasyRoleSkillsEn, CyberRoleSkillsEn, ModernRoleSkillsEn)
        : Genre.Pick(FantasyRoleSkills, CyberRoleSkills, ModernRoleSkills);

    /// <summary>Английские шаблонные навыки ролей — те же числа, что у русских.</summary>
    private static readonly Dictionary<string, (string Name, string Cat, string Target, string Desc)[]> FantasyRoleSkillsEn = new()
    {
        ["tank"] = new[]
        {
            ("Taunt", "debuff", "enemy", "The enemy must attack the defender for 1 round (WIS saving throw against the skill DC)."),
            ("Shield an Ally", "shield", "ally", "A shield of {K}+CON points on an ally until the start of the defender's next turn."),
            ("Shield Bash", "attack", "enemy", "{K}+STR bludgeoning and stun for 1 round (CON saving throw)."),
            ("Steadfast", "support", "self", "Resistance to physical damage for 2 rounds; cooldown 3 rounds."),
        },
        ["striker"] = new[]
        {
            ("Power Strike", "attack", "enemy", "A strike for {K}+STR weapon damage."),
            ("Whirlwind", "attack", "all-enemies", "{K} damage to up to three adjacent enemies (an attack against each)."),
            ("Battle Cry", "support", "all-allies", "+2 to the party's attacks for 2 rounds."),
            ("Bloodletting", "debuff", "enemy", "{K} damage and bleeding 1d6 for 3 rounds (CON saving throw)."),
        },
        ["ranged"] = new[]
        {
            ("Aimed Shot", "attack", "enemy", "A shot with advantage: {K}+DEX piercing."),
            ("Volley", "attack", "all-enemies", "{K} piercing to 2–3 targets."),
            ("Pinning Arrow", "debuff", "enemy", "{K} damage and entangle for 1 round (STR saving throw)."),
            ("Hunter's Mark", "debuff", "enemy", "The party deals +1d6 damage to the marked target for 3 rounds."),
        },
        ["skirmisher"] = new[]
        {
            ("Shadow Strike", "attack", "enemy", "From stealth or when flanking: {K}+DEX damage."),
            ("Poisoned Blade", "debuff", "enemy", "{K} damage and poison 1d6 for 3 rounds (CON saving throw)."),
            ("Evasion", "support", "self", "Reaction: half damage from an attack or an area; once per round."),
            ("Fan of Knives", "attack", "all-enemies", "{K} piercing to 2–3 targets."),
        },
        ["caster"] = new[]
        {
            ("Fire Bolt", "attack", "enemy", "{K}+INT fire; the target catches fire (burning 1d6, 2 rounds)."),
            ("Frost Shackles", "debuff", "enemy", "{K} cold and slow for 2 rounds (CON saving throw)."),
            ("Fireball", "attack", "all-enemies", "{K} fire in an area (DEX saving throw — half)."),
            ("Arcane Shield", "shield", "self", "A shield of {K}+INT points for 3 rounds."),
        },
        ["healer"] = new[]
        {
            ("Heal", "heal", "ally", "Restores {K}+WIS HP to an ally."),
            ("Cleanse", "cleanse", "ally", "Removes 1 debuff (from tier 3 — 2)."),
            ("Blessing", "support", "all-allies", "+1d4 to the party's attacks and saving throws for 3 rounds."),
            ("Holy Light", "attack", "enemy", "{K}+WIS radiant; undead take damage with advantage."),
        },
        ["support"] = new[]
        {
            ("Inspiration", "support", "ally", "An ally gets +2 to attack and damage for 3 rounds."),
            ("Healing Chant", "heal", "all-allies", "The party restores {K} HP."),
            ("Mockery", "debuff", "enemy", "{K} psychic damage and disadvantage on the next attack (WIS saving throw)."),
            ("Song of Fortitude", "shield", "all-allies", "A shield of {K} points on the party for 2 rounds."),
        },
        ["controller"] = new[]
        {
            ("Sleep", "debuff", "enemy", "Sleep for up to 2 rounds, ends on damage (WIS saving throw)."),
            ("Shadow Bonds", "debuff", "enemy", "{K} necrotic and entangle for 1 round (STR saving throw)."),
            ("Confusion", "debuff", "enemy", "Confusion for 2 rounds (WIS saving throw)."),
            ("Mind Blast", "attack", "enemy", "{K}+INT psychic damage."),
        },
    };

    private static readonly Dictionary<string, (string Name, string Cat, string Target, string Desc)[]> CyberRoleSkillsEn = new()
    {
        ["tank"] = new[]
        {
            ("Draw Fire", "debuff", "enemy", "The enemy must attack the defender for 1 round (WIS saving throw against the skill DC)."),
            ("Cover with a Shield", "shield", "ally", "A shield of {K}+CON points on an ally until the start of the defender's next turn."),
            ("Shield Ram", "attack", "enemy", "{K}+STR blunt and stun for 1 round (CON saving throw)."),
            ("Armor Up", "support", "self", "Resistance to kinetic damage for 2 rounds; cooldown 3 rounds."),
        },
        ["striker"] = new[]
        {
            ("Crushing Blow", "attack", "enemy", "A strike for {K}+STR weapon damage."),
            ("Flurry of Blows", "attack", "all-enemies", "{K} damage to up to three adjacent enemies (an attack against each)."),
            ("Battle Roar", "support", "all-allies", "+2 to the crew's attacks for 2 rounds."),
            ("Ragged Wound", "debuff", "enemy", "{K} damage and bleeding 1d6 for 3 rounds (CON saving throw)."),
        },
        ["ranged"] = new[]
        {
            ("Aimed Shot", "attack", "enemy", "A shot with advantage: {K}+DEX kinetic."),
            ("Burst", "attack", "all-enemies", "{K} kinetic to 2–3 targets."),
            ("Kneecap Shot", "debuff", "enemy", "{K} damage and immobilized for 1 round (STR saving throw)."),
            ("Target Mark", "debuff", "enemy", "The crew deals +1d6 damage to the marked target for 3 rounds."),
        },
        ["skirmisher"] = new[]
        {
            ("Strike from the Shadows", "attack", "enemy", "From stealth or the flank: {K}+DEX damage."),
            ("Neurotoxin Blade", "debuff", "enemy", "{K} damage and toxin 1d6 for 3 rounds (CON saving throw)."),
            ("Reflex Dodge", "support", "self", "Reaction: half damage from an attack or an explosion; once per round."),
            ("Throwing Knives", "attack", "all-enemies", "{K} slashing to 2–3 targets."),
        },
        ["caster"] = new[]
        {
            ("Implant Overheat", "attack", "enemy", "{K}+INT thermal through the net; the target burns (1d6, 2 rounds)."),
            ("Optics Glitch", "debuff", "enemy", "{K} cyberattack and blindness for 2 rounds (INT saving throw)."),
            ("Short Circuit", "attack", "all-enemies", "{K} EMP to all connected enemies (CON saving throw — half)."),
            ("Firewall", "shield", "self", "A shield of {K}+INT points for 3 rounds."),
        },
        ["healer"] = new[]
        {
            ("Medstim", "heal", "ally", "Restores {K}+WIS HP to an ally."),
            ("System Reboot", "cleanse", "ally", "Removes 1 debuff (from tier 3 — 2)."),
            ("Tactical Sync", "support", "all-allies", "+1d4 to the crew's attacks and saving throws for 3 rounds."),
            ("Shock Discharge", "attack", "enemy", "{K}+WIS EMP; drones and cyborgs take damage with advantage."),
        },
        ["support"] = new[]
        {
            ("Booster Drone", "support", "ally", "An ally gets +2 to attack and damage for 3 rounds."),
            ("Nanoswarm", "heal", "all-allies", "The crew restores {K} HP."),
            ("Jamming", "debuff", "enemy", "{K} cyberattack and disadvantage on the next attack (INT saving throw)."),
            ("Field Projectors", "shield", "all-allies", "A shield of {K} points on the crew for 2 rounds."),
        },
        ["controller"] = new[]
        {
            ("Shutdown", "debuff", "enemy", "The target shuts down for up to 2 rounds, ends on damage (INT saving throw)."),
            ("Cyber Grip", "debuff", "enemy", "{K} EMP and grapple for 1 round (STR saving throw)."),
            ("Holo Decoys", "debuff", "enemy", "Navigation glitch for 2 rounds (INT saving throw)."),
            ("Neural Strike", "attack", "enemy", "{K}+INT cyberattack."),
        },
    };

    private static readonly Dictionary<string, (string Name, string Cat, string Target, string Desc)[]> ModernRoleSkillsEn = new()
    {
        ["tank"] = new[]
        {
            ("Draw Fire", "debuff", "enemy", "The enemy must attack the defender for 1 round (WIS saving throw against the skill DC)."),
            ("Cover with a Shield", "shield", "ally", "A shield of {K}+CON points on an ally until the start of the defender's next turn."),
            ("Shield Bash", "attack", "enemy", "{K}+STR blunt and stun for 1 round (CON saving throw)."),
            ("Take the Hit", "support", "self", "Resistance to ballistic and blunt for 2 rounds; cooldown 3 rounds."),
        },
        ["striker"] = new[]
        {
            ("Power Strike", "attack", "enemy", "A strike for {K}+STR weapon damage."),
            ("Sweeping Blow", "attack", "all-enemies", "{K} damage to up to three adjacent enemies (an attack against each)."),
            ("Fury", "support", "all-allies", "+2 to the team's attacks for 2 rounds."),
            ("Ragged Wound", "debuff", "enemy", "{K} damage and bleeding 1d6 for 3 rounds (CON saving throw)."),
        },
        ["ranged"] = new[]
        {
            ("Aimed Shot", "attack", "enemy", "A shot with advantage: {K}+DEX ballistic."),
            ("Burst", "attack", "all-enemies", "{K} ballistic to 2–3 targets."),
            ("Leg Shot", "debuff", "enemy", "{K} damage and immobilized for 1 round (STR saving throw)."),
            ("Target Mark", "debuff", "enemy", "The team deals +1d6 damage to the marked target for 3 rounds."),
        },
        ["skirmisher"] = new[]
        {
            ("Sneak Attack", "attack", "enemy", "From cover or the flank: {K}+DEX damage."),
            ("Knife to a Weak Spot", "debuff", "enemy", "{K} damage and bleeding 1d6 for 3 rounds (CON saving throw)."),
            ("Dodge", "support", "self", "Reaction: half damage from an attack or an explosion; once per round."),
            ("Throw Knives", "attack", "all-enemies", "{K} piercing to 2–3 targets."),
        },
        ["caster"] = new[]
        {
            ("Tech Overload", "attack", "enemy", "{K}+INT electric through a hacked device near the target."),
            ("Blind the Cameras and Lights", "debuff", "enemy", "{K} electric and blindness for 2 rounds (CON saving throw)."),
            ("Wiring Short Circuit", "attack", "all-enemies", "{K} electric to everyone near wiring and machinery (DEX saving throw — half)."),
            ("Blind Spot", "shield", "self", "A shield of {K}+INT points for 3 rounds: enemies lose sight of the hero."),
        },
        ["healer"] = new[]
        {
            ("Bandaging", "heal", "ally", "Restores {K}+WIS HP to an ally."),
            ("Painkiller", "cleanse", "ally", "Removes 1 debuff (from tier 3 — 2)."),
            ("Pull Yourselves Together!", "support", "all-allies", "+1d4 to the team's attacks and saving throws for 3 rounds."),
            ("Stun Gun", "attack", "enemy", "{K}+WIS electric; on a hit — disadvantage on the target's next attack."),
        },
        ["support"] = new[]
        {
            ("Spotter Drone", "support", "ally", "An ally gets +2 to attack and damage for 3 rounds."),
            ("Field Kit", "heal", "all-allies", "The team restores {K} HP."),
            ("Signal Jammer", "debuff", "enemy", "{K} drone damage and disadvantage on the next attack (INT saving throw)."),
            ("Mobile Cover", "shield", "all-allies", "A shield of {K} points on the team for 2 rounds."),
        },
        ["controller"] = new[]
        {
            ("Flashbang", "debuff", "enemy", "Stun for up to 2 rounds, ends on damage (CON saving throw)."),
            ("Grapple and Cuffs", "debuff", "enemy", "{K} blunt and grapple for 1 round (STR saving throw)."),
            ("Psychological Pressure", "debuff", "enemy", "Panic for 2 rounds (WIS saving throw)."),
            ("Taser Discharge", "attack", "enemy", "{K}+INT electric."),
        },
    };

    /// <summary>Шаблонные навыки ролей в современности: приземлённые приёмы бойцов, медиков и технарей.</summary>
    private static readonly Dictionary<string, (string Name, string Cat, string Target, string Desc)[]> ModernRoleSkills = new()
    {
        ["tank"] = new[]
        {
            ("Принять огонь на себя", "debuff", "enemy", "Враг обязан атаковать защитника 1 раунд (спасбросок МДР против DC навыков)."),
            ("Прикрыть щитом", "shield", "ally", "Щит союзнику на {K}+ТЕЛ очков до начала следующего хода защитника."),
            ("Удар щитом", "attack", "enemy", "{K}+СИЛ ударного и оглушение на 1 раунд (спасбросок ТЕЛ)."),
            ("Держать удар", "support", "self", "Сопротивление огнестрельному и ударному на 2 раунда; перезарядка 3 раунда."),
        },
        ["striker"] = new[]
        {
            ("Мощный удар", "attack", "enemy", "Удар на {K}+СИЛ урона оружием."),
            ("Размашистый удар", "attack", "all-enemies", "{K} урона до трёх врагов рядом (атака против каждого)."),
            ("Злость", "support", "all-allies", "+2 к атаке команды на 2 раунда."),
            ("Рваная рана", "debuff", "enemy", "{K} урона и кровотечение 1d6 на 3 раунда (спасбросок ТЕЛ)."),
        },
        ["ranged"] = new[]
        {
            ("Прицельный выстрел", "attack", "enemy", "Выстрел с преимуществом: {K}+ЛОВ огнестрельного."),
            ("Очередь", "attack", "all-enemies", "{K} огнестрельного по 2–3 целям."),
            ("Выстрел по ногам", "debuff", "enemy", "{K} урона и обездвиживание на 1 раунд (спасбросок СИЛ)."),
            ("Метка цели", "debuff", "enemy", "По отмеченной цели команда наносит +1d6 урона 3 раунда."),
        },
        ["skirmisher"] = new[]
        {
            ("Удар исподтишка", "attack", "enemy", "Из укрытия или с фланга: {K}+ЛОВ урона."),
            ("Нож в уязвимое место", "debuff", "enemy", "{K} урона и кровотечение 1d6 на 3 раунда (спасбросок ТЕЛ)."),
            ("Увернуться", "support", "self", "Реакция: половина урона от атаки или взрыва; раз в раунд."),
            ("Метнуть ножи", "attack", "all-enemies", "{K} колющего по 2–3 целям."),
        },
        ["caster"] = new[]
        {
            ("Перегрузка техники", "attack", "enemy", "{K}+ИНТ электричеством через взломанное устройство рядом с целью."),
            ("Ослепить камеры и свет", "debuff", "enemy", "{K} электричеством и ослепление на 2 раунда (спасбросок ТЕЛ)."),
            ("Короткое замыкание проводки", "attack", "all-enemies", "{K} электричеством по всем у проводки и техники (спасбросок ЛОВ — половина)."),
            ("Слепая зона", "shield", "self", "Щит на {K}+ИНТ очков на 3 раунда: враги теряют героя из виду."),
        },
        ["healer"] = new[]
        {
            ("Перевязка", "heal", "ally", "Восстанавливает {K}+МДР ХП союзнику."),
            ("Обезболивающее", "cleanse", "ally", "Снимает 1 дебаф (с 3-й ступени — 2)."),
            ("Собраться!", "support", "all-allies", "+1d4 к атакам и спасброскам команды на 3 раунда."),
            ("Шокер", "attack", "enemy", "{K}+МДР электричеством; при попадании — помеха на следующую атаку цели."),
        },
        ["support"] = new[]
        {
            ("Дрон-корректировщик", "support", "ally", "Союзник получает +2 к атаке и урону на 3 раунда."),
            ("Полевая аптечка", "heal", "all-allies", "Команда восстанавливает {K} ХП."),
            ("Глушилка связи", "debuff", "enemy", "{K} урона дроном и помеха на следующую атаку (спасбросок ИНТ)."),
            ("Мобильные укрытия", "shield", "all-allies", "Щит команде на {K} очков на 2 раунда."),
        },
        ["controller"] = new[]
        {
            ("Светошумовая", "debuff", "enemy", "Оглушение до 2 раундов, спадает от урона (спасбросок ТЕЛ)."),
            ("Захват и наручники", "debuff", "enemy", "{K} ударного и захват на 1 раунд (спасбросок СИЛ)."),
            ("Психологическое давление", "debuff", "enemy", "Паника на 2 раунда (спасбросок МДР)."),
            ("Разряд шокера", "attack", "enemy", "{K}+ИНТ электричеством."),
        },
    };

    /// <summary>Шаблонные навыки ролей в киберпанке: та же механика, другие приёмы.</summary>
    private static readonly Dictionary<string, (string Name, string Cat, string Target, string Desc)[]> CyberRoleSkills = new()
    {
        ["tank"] = new[]
        {
            ("Вызвать огонь на себя", "debuff", "enemy", "Враг обязан атаковать защитника 1 раунд (спасбросок МДР против DC навыков)."),
            ("Прикрыть щитом", "shield", "ally", "Щит союзнику на {K}+ТЕЛ очков до начала следующего хода защитника."),
            ("Таран щитом", "attack", "enemy", "{K}+СИЛ ударного и оглушение на 1 раунд (спасбросок ТЕЛ)."),
            ("Бронирование", "support", "self", "Сопротивление кинетическому урону на 2 раунда; перезарядка 3 раунда."),
        },
        ["striker"] = new[]
        {
            ("Сокрушающий удар", "attack", "enemy", "Удар на {K}+СИЛ урона оружием."),
            ("Шквал ударов", "attack", "all-enemies", "{K} урона до трёх врагов рядом (атака против каждого)."),
            ("Боевой рёв", "support", "all-allies", "+2 к атаке команды на 2 раунда."),
            ("Рваная рана", "debuff", "enemy", "{K} урона и кровотечение 1d6 на 3 раунда (спасбросок ТЕЛ)."),
        },
        ["ranged"] = new[]
        {
            ("Прицельный выстрел", "attack", "enemy", "Выстрел с преимуществом: {K}+ЛОВ кинетического."),
            ("Очередь", "attack", "all-enemies", "{K} кинетического по 2–3 целям."),
            ("Выстрел в колено", "debuff", "enemy", "{K} урона и обездвиживание на 1 раунд (спасбросок СИЛ)."),
            ("Метка цели", "debuff", "enemy", "По отмеченной цели команда наносит +1d6 урона 3 раунда."),
        },
        ["skirmisher"] = new[]
        {
            ("Удар из тени", "attack", "enemy", "Из стелса или с фланга: {K}+ЛОВ урона."),
            ("Клинок с нейротоксином", "debuff", "enemy", "{K} урона и токсин 1d6 на 3 раунда (спасбросок ТЕЛ)."),
            ("Рефлекс-уклонение", "support", "self", "Реакция: половина урона от атаки или взрыва; раз в раунд."),
            ("Метательные ножи", "attack", "all-enemies", "{K} режущего по 2–3 целям."),
        },
        ["caster"] = new[]
        {
            ("Перегрев имплантов", "attack", "enemy", "{K}+ИНТ термическим через сеть; цель горит (1d6, 2 раунда)."),
            ("Сбой оптики", "debuff", "enemy", "{K} кибератакой и ослепление на 2 раунда (спасбросок ИНТ)."),
            ("Короткое замыкание", "attack", "all-enemies", "{K} ЭМИ по всем подключённым врагам (спасбросок ТЕЛ — половина)."),
            ("Файрвол", "shield", "self", "Щит на {K}+ИНТ очков на 3 раунда."),
        },
        ["healer"] = new[]
        {
            ("Медстим", "heal", "ally", "Восстанавливает {K}+МДР ХП союзнику."),
            ("Перезагрузка систем", "cleanse", "ally", "Снимает 1 дебаф (с 3-й ступени — 2)."),
            ("Тактическая синхронизация", "support", "all-allies", "+1d4 к атакам и спасброскам команды на 3 раунда."),
            ("Шоковый разряд", "attack", "enemy", "{K}+МДР ЭМИ; дроны и киборги — с преимуществом урона."),
        },
        ["support"] = new[]
        {
            ("Дрон-усилитель", "support", "ally", "Союзник получает +2 к атаке и урону на 3 раунда."),
            ("Нанорой", "heal", "all-allies", "Команда восстанавливает {K} ХП."),
            ("Помехи", "debuff", "enemy", "{K} кибератакой и помеха на следующую атаку (спасбросок ИНТ)."),
            ("Проекторы поля", "shield", "all-allies", "Щит команде на {K} очков на 2 раунда."),
        },
        ["controller"] = new[]
        {
            ("Отключение", "debuff", "enemy", "Цель отключается до 2 раундов, спадает от урона (спасбросок ИНТ)."),
            ("Кибер-хват", "debuff", "enemy", "{K} ЭМИ и захват на 1 раунд (спасбросок СИЛ)."),
            ("Голо-приманки", "debuff", "enemy", "Сбой навигации на 2 раунда (спасбросок ИНТ)."),
            ("Нейроудар", "attack", "enemy", "{K}+ИНТ кибератакой."),
        },
    };

    private static readonly Dictionary<string, (string Name, string Cat, string Target, string Desc)[]> FantasyRoleSkills = new()
    {
        ["tank"] = new[]
        {
            ("Провокация", "debuff", "enemy", "Враг обязан атаковать защитника 1 раунд (спасбросок МДР против DC навыков)."),
            ("Щит союзнику", "shield", "ally", "Щит союзнику на {K}+ТЕЛ очков до начала следующего хода защитника."),
            ("Удар щитом", "attack", "enemy", "{K}+СИЛ дробящего и оглушение на 1 раунд (спасбросок ТЕЛ)."),
            ("Непоколебимость", "support", "self", "Сопротивление физическому урону на 2 раунда; перезарядка 3 раунда."),
        },
        ["striker"] = new[]
        {
            ("Мощный удар", "attack", "enemy", "Удар на {K}+СИЛ урона оружием."),
            ("Круговой взмах", "attack", "all-enemies", "{K} урона до трёх врагов рядом (атака против каждого)."),
            ("Боевой клич", "support", "all-allies", "+2 к атаке группы на 2 раунда."),
            ("Кровопускание", "debuff", "enemy", "{K} урона и кровотечение 1d6 на 3 раунда (спасбросок ТЕЛ)."),
        },
        ["ranged"] = new[]
        {
            ("Прицельный выстрел", "attack", "enemy", "Выстрел с преимуществом: {K}+ЛОВ колющего."),
            ("Залп", "attack", "all-enemies", "{K} колющего по 2–3 целям."),
            ("Пригвождающая стрела", "debuff", "enemy", "{K} урона и опутывание на 1 раунд (спасбросок СИЛ)."),
            ("Метка охотника", "debuff", "enemy", "По отмеченной цели группа наносит +1d6 урона 3 раунда."),
        },
        ["skirmisher"] = new[]
        {
            ("Удар из тени", "attack", "enemy", "Из скрытности или при фланкировании: {K}+ЛОВ урона."),
            ("Отравленный клинок", "debuff", "enemy", "{K} урона и отравление 1d6 на 3 раунда (спасбросок ТЕЛ)."),
            ("Уклонение", "support", "self", "Реакция: половина урона от атаки или области; раз в раунд."),
            ("Веер ножей", "attack", "all-enemies", "{K} колющего по 2–3 целям."),
        },
        ["caster"] = new[]
        {
            ("Огненная стрела", "attack", "enemy", "{K}+ИНТ огнём; цель загорается (поджог 1d6, 2 раунда)."),
            ("Ледяные оковы", "debuff", "enemy", "{K} холодом и замедление на 2 раунда (спасбросок ТЕЛ)."),
            ("Огненный шар", "attack", "all-enemies", "{K} огнём по области (спасбросок ЛОВ — половина)."),
            ("Магический щит", "shield", "self", "Щит на {K}+ИНТ очков на 3 раунда."),
        },
        ["healer"] = new[]
        {
            ("Исцеление", "heal", "ally", "Восстанавливает {K}+МДР ХП союзнику."),
            ("Очищение", "cleanse", "ally", "Снимает 1 дебаф (с 3-й ступени — 2)."),
            ("Благословение", "support", "all-allies", "+1d4 к атакам и спасброскам группы на 3 раунда."),
            ("Священный свет", "attack", "enemy", "{K}+МДР светом; нежить — с преимуществом урона."),
        },
        ["support"] = new[]
        {
            ("Вдохновение", "support", "ally", "Союзник получает +2 к атаке и урону на 3 раунда."),
            ("Лечебный напев", "heal", "all-allies", "Группа восстанавливает {K} ХП."),
            ("Насмешка", "debuff", "enemy", "{K} психического урона и помеха на следующую атаку (спасбросок МДР)."),
            ("Песнь стойкости", "shield", "all-allies", "Щит группе на {K} очков на 2 раунда."),
        },
        ["controller"] = new[]
        {
            ("Усыпление", "debuff", "enemy", "Сон до 2 раундов, спадает от урона (спасбросок МДР)."),
            ("Путы теней", "debuff", "enemy", "{K} некротикой и опутывание на 1 раунд (спасбросок СИЛ)."),
            ("Замешательство", "debuff", "enemy", "Замешательство на 2 раунда (спасбросок МДР)."),
            ("Разряд разума", "attack", "enemy", "{K}+ИНТ психического урона."),
        },
    };

    private static readonly int[] RankMana = { 3, 5, 9, 15, 24 };
    private static string[] RankPower => Lang.IsEn ? new[] { "1K", "1.5K", "2K", "3K", "4K" } : new[] { "1К", "1.5К", "2К", "3К", "4К" };

    private static SkillRecord SkillFor(string name, string cat, string target, string desc, int rank, string? icon = null) => new()
    {
        Name = name, Category = cat, Target = target, Rank = rank, ManaCost = RankMana[rank - 1],
        Description = desc.Replace("{K}", RankPower[rank - 1]).Replace("{R}", rank.ToString(CultureInfo.InvariantCulture)),
        Icon = icon ?? "",
    };

    private (bool Ok, string Result) RecruitCompanion(string? chatId, JsonObject args)
    {
        if (chatId is null) return (false, L("Ошибка: инструмент доступен только внутри открытого чата.", "Error: the tool is available only inside an open chat."));
        var name = (Str(args, "name") ?? "").Trim();
        if (name.Length == 0) return (false, L("Ошибка: нужно name.", "Error: name is required."));
        var state = _rpg.GetOrCreate(chatId);
        if (state.Party.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return (false, L($"Ошибка: «{name}» уже в группе — меняй его через update_party_member / update_skill.", $"Error: \"{name}\" is already in the party — change them via update_party_member / update_skill."));
        if (state.Party.Count >= 3) return (false, L("Ошибка: в группе уже три спутника — это максимум.", "Error: the party already has three companions — that is the maximum."));

        var candidate = state.CompanionCandidates.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (candidate is null || candidate.Stage != "ready" || !candidate.RoleOffered)
            return (false, L($"Ошибка: «{name}» нельзя нанять напрямую. Нужны stage=ready и уже показанный ask_companion_role после выбора игрока.", $"Error: \"{name}\" cannot be hired directly. stage=ready and an already shown ask_companion_role after the player's choice are required."));

        var roleRaw = (Str(args, "role") ?? "").Trim();
        var arch = Progression.Archetypes.FirstOrDefault(a => a.Key.Equals(roleRaw, StringComparison.OrdinalIgnoreCase) ||
                                                              roleRaw.Contains($"[{a.Key}]", StringComparison.OrdinalIgnoreCase) ||
                                                              a.Title.Equals(roleRaw, StringComparison.OrdinalIgnoreCase) ||
                                                              a.Ask.Equals(roleRaw, StringComparison.OrdinalIgnoreCase))
                   ?? Progression.GuessArchetype(roleRaw + " " + Str(args, "class"));
        var heroLevel = PartyLevel(state);
        var level = Math.Clamp(Num(args, "level") ?? heroLevel, 1, heroLevel);

        // Характеристики: стандартный набор по приоритету роли + рост на уровнях характеристик.
        int[] array = { 15, 14, 13, 12, 10, 8 };
        var member = new PartyMember
        {
            Name = name, Role = arch.Key, Level = level.ToString(CultureInfo.InvariantCulture),
            CharClass = (Str(args, "class") ?? arch.ClassIdeas[0]).Trim(),
            Race = (Str(args, "race") ?? candidate.Race).Trim(), Gender = (Str(args, "gender") ?? candidate.Gender).Trim(),
            Note = (Str(args, "personality") ?? L($"Мотив: {candidate.Motivation} Личная цель: {candidate.PersonalGoal}", $"Motive: {candidate.Motivation} Personal goal: {candidate.PersonalGoal}")).Trim(),
        };
        for (var i = 0; i < 6; i++) member.SetStat(arch.Priority[i], array[i]);
        for (var l = 2; l <= level; l++)
        {
            if (!Progression.IsStatLevel(l)) continue;
            foreach (var st in arch.Priority.Take(2)) member.SetStat(st, Math.Min(20, member.Stat(st) + 1));
        }

        var conMod = DndStatNames.Modifier(member.Con);
        var keyMod = DndStatNames.Modifier(member.Stat(arch.Priority[0]));
        member.HpMax = member.HpCurrent = Progression.CompanionHpAtLevel(arch, conMod, level);
        member.ManaMax = member.ManaCurrent = Progression.ManaAtLevel(arch, keyMod, level);

        // Снаряжение по уровню: обычное на 1–4, добротное на 5–9, редкое на 10–14, эпическое с 15-го.
        var rarity = level < 5 ? "common" : level < 10 ? "uncommon" : level < 15 ? "rare" : "epic";
        var gear = new List<string> { arch.Weapon, arch.Armor };
        if (Genre.IsFantasy ? arch.Key is "tank" or "healer" : arch.Key == "tank") gear.Add(Genre.Pick("shield", "cy_ballistic", "md_riot_shield"));
        var gearText = new List<string>();
        foreach (var id in gear)
        {
            if (_catalog.Get(id) is not { } def || def.EquipSlot is not { } slot) continue;
            var item = new GridItem { Name = def.Name, Icon = def.Id, W = def.W, H = def.H, Slot = slot, TwoHanded = def.TwoHanded, Rarity = rarity, Level = level };
            ItemStats.ApplyBase(item);
            ItemStats.RollBonuses(item, Random.Shared);
            ItemStats.Balance(item);
            if (rarity != "common") item.Name = $"{def.Name} ({ItemEconomy.RarityTitle(rarity)})";
            var worn = new EquippedItem
            {
                Name = item.Name, Icon = item.Icon, W = item.W, H = item.H, TwoHanded = item.TwoHanded, Value = _economy.UnitValue(item),
                Rarity = item.Rarity, Damage = item.Damage, Armor = item.Armor, Bonuses = new(item.Bonuses), Effects = new(item.Effects), Level = item.Level,
                Note = ItemEconomy.RarityNote(rarity),
            };
            member.Equipment[slot.ToString()] = worn;
            if (worn.TwoHanded) member.Equipment[nameof(EquipSlot.Hand2)] = worn;
            gearText.Add($"{worn.Name}{(ItemStats.HasStats(worn) ? $" {{{ItemStats.Describe(worn)}}}" : "")}");
        }

        // Пара зелий в сумке.
        var potion = new GridItem { Name = Lang.IsEn
            ? Genre.Pick(level < 5 ? "Minor Healing Potion" : level < 11 ? "Healing Potion" : "Greater Healing Potion", level < 5 ? "Medstim" : level < 11 ? "Medstim+" : "Military Medstim", level < 5 ? "Bandages and Painkillers" : level < 11 ? "First Aid Kit" : "Army First Aid Kit")
            : Genre.Pick(level < 5 ? "Малое зелье лечения" : level < 11 ? "Зелье лечения" : "Большое зелье лечения", level < 5 ? "Медстим" : level < 11 ? "Медстим+" : "Военный медстим", level < 5 ? "Бинты и обезболивающее" : level < 11 ? "Аптечка" : "Армейская аптечка"),
            Icon = Genre.Pick("potion_red", "cy_medstim", "md_medkit"), Quantity = 2, Consumable = true,
            Note = level < 5 ? L("Восстанавливает 2d4+2 ХП.", "Restores 2d4+2 HP.") : level < 11 ? L("Восстанавливает 4d4+4 ХП.", "Restores 4d4+4 HP.") : L("Восстанавливает 8d4+8 ХП.", "Restores 8d4+8 HP."), Level = level };
        if (InventoryOps.FindFree(member.Grid, 1, 1) is { } spot) { (potion.Col, potion.Row) = spot; member.Grid.Add(potion); }

        // Навыки: свои (ступень и мана по бюджету) или шаблоны роли.
        var cap = Progression.MaxSkillRank(level);
        var count = Math.Min(6, 2 + level / 4);
        var custom = (args["skills"] as JsonArray)?.OfType<JsonObject>().Where(o => (Str(o, "name") ?? "").Trim().Length > 0).ToList() ?? new();
        for (var i = 0; i < count; i++)
        {
            var rank = Math.Max(1, i == 0 ? cap : cap - 1);
            if (i < custom.Count)
            {
                var o = custom[i];
                member.LearnedSkills.Add(SkillFor(Str(o, "name")!.Trim(), (Str(o, "category") ?? "attack").Trim().ToLowerInvariant(),
                    (Str(o, "target") ?? "enemy").Trim(), (Str(o, "description") ?? "").Trim(), rank, Str(o, "icon")));
            }
            else if (custom.Count == 0 && i < RoleSkills[arch.Key].Length)
            {
                var t = RoleSkills[arch.Key][i];
                member.LearnedSkills.Add(SkillFor(t.Name, t.Cat, t.Target, t.Desc, rank));
            }
        }

        member.Portrait = PortraitCatalog.ResolvePerson(Str(args, "portrait") ?? candidate.Portrait, member.Race, member.Gender, member.CharClass, member.Name)?.Id ?? "";
        state.Party.Add(member);
        candidate.Stage = "recruited";
        candidate.SelectedRole = arch.Key;
        state.ImportantCharacters.RemoveAll(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        state.Notables.RemoveAll(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        _rpg.Save(chatId, state);

        var atk = ItemStats.BestAttack(member);
        var skillsText = string.Join("; ", member.LearnedSkills.Select(s => $"{s.Name} {L("ст.", "t.")}{s.Rank} ({s.ManaCost} {L("маны", "mana")}) — {s.Description}"));
        return (true, L(
            $"OK: «{name}» в группе ({state.Party.Count}/3): {member.CharClass}, роль «{arch.Title}», ур. {level}. " +
            $"ХП {member.HpMax}, мана {member.ManaMax}, КБ {ItemStats.ArmorClass(member)}, {atk.Title}: атака {atk.Attack:+#;-#;+0}, урон {atk.DamageText}. " +
            $"Характеристики: СИЛ {member.Str}, ЛОВ {member.Dex}, ТЕЛ {member.Con}, ИНТ {member.Int}, МДР {member.Wis}, ХАР {member.Cha}. " +
            $"Снаряжение: {string.Join("; ", gearText)}; в сумке {potion.Name} x2. " +
            $"Навыки: {skillsText}. " +
            $"Портрет: {member.Portrait}. Навыки можно переименовать и переописать под мир через update_skill, не превышая ступень {cap}. " +
            "Представь спутника игроку в сцене коротко, без таблицы чисел.",
            $"OK: \"{name}\" is in the party ({state.Party.Count}/3): {member.CharClass}, role \"{arch.Title}\", level {level}. " +
            $"HP {member.HpMax}, mana {member.ManaMax}, AC {ItemStats.ArmorClass(member)}, {atk.Title}: attack {atk.Attack:+#;-#;+0}, damage {atk.DamageText}. " +
            $"Ability scores: STR {member.Str}, DEX {member.Dex}, CON {member.Con}, INT {member.Int}, WIS {member.Wis}, CHA {member.Cha}. " +
            $"Gear: {string.Join("; ", gearText)}; in the bag {potion.Name} x2. " +
            $"Skills: {skillsText}. " +
            $"Portrait: {member.Portrait}. The skills can be renamed and redescribed for the world via update_skill without exceeding tier {cap}. " +
            "Introduce the companion to the player in the scene briefly, without a table of numbers."));
    }
}
