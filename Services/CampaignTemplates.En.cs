namespace RPG_Harness.Services;

/// <summary>
/// Английские шаблоны файлов кампании (все три сеттинга). Механика совпадает с русскими шаблонами —
/// правя правила в одном языке, правьте и другой (и промпты, и docs/CombatSystem.md).
/// </summary>
public static partial class CampaignTemplates
{
    public const string WorldEn = """
        # World: <name>

        > Campaign template. Fill in the sections, keeping the headings; delete what stays empty, add your own.

        ## In brief
        _2–3 sentences: what this world is about and what makes it gripping._

        ## Genre and tone
        - **Genre:** _fantasy / dark fantasy / steampunk / …_
        - **Tone:** _heroic / grim / humorous / unsettling…_
        - **Boundaries:** _what we avoid, how harsh it gets._

        ## Magic and technology
        - **Magic:** _rare and dangerous / a craft / everywhere; its source and price._
        - **Technology:** _the era, key inventions._

        ## Geography
        _Regions and natural zones — one line each._

        ## Settlements
        _The main cities and villages — one line each (details go to Cities.md via update_city)._

        ## Factions
        _In brief; in detail — in Factions.md._

        ## Economy and prices
        - **Currency:** _gold (g.)._
        - **Reference prices:** _a night's lodging 1 g., a meal 1 g., a sword 15 g., chain mail 75 g., a healing potion 25 g., a horse 75 g._
        - **Scarcity and plenty:** _what is expensive here and what is cheap._

        ## Religion and culture
        _Gods, customs, holidays, taboos._

        ## History
        _3–5 past events that shape the present._

        ## Rumors
        _What they gossip about in taverns — part truth, part lies._
        """;

    public const string RulesEn = """
        # Game rules

        > Core rules in the spirit of D&D 5e. The GM extends them with the world's own rules (the "Special rules" section).

        ## Checks
        - **d20 + modifier** against the **Difficulty Class (DC)**; modifier = (ability − 10) / 2 rounded down.
        - DC: 5 — very easy, 10 — easy, 15 — medium, 20 — hard, 25 — very hard, 30 — nearly impossible.
        - Advantage / disadvantage: roll two d20, take the higher / lower.
        - Degrees of success (`skill_check`): 10 or more above the DC — brilliant success; meeting the DC — success; 1–3 below the DC — success at a cost (the goal is reached with a complication, the player may back off); lower — failure. A natural 20 raises the result one step, a natural 1 lowers it.

        ## Combat
        - Initiative: d20 + DEX, rolled once at the start of combat; `combat_turn` keeps track of the current participant and the round number.
        - The player's turn is their turn in initiative order: companions and adversaries act in the same turn until the round ends.
        - Attack: d20 + attack bonus against the target's AC. A natural 20 is a critical hit (damage dice are doubled), a natural 1 is a miss.
        - Damage: the weapon die + modifier (STR — melee, DEX — ranged and finesse) + item bonuses.
        - Advantage / disadvantage: roll two d20, take the better / worse.
        - Saving throw: d20 + modifier against the effect's DC. Skill DC = 8 + proficiency + key ability modifier.
        - HP down to 0 — "dying": unconscious; death saves are made by `resolve_death_save`, which keeps the score (3 successes — stabilized, 3 failures — death, a natural 20 — comes to with 1 HP).
        - If the hero cannot act (0 HP, stun, sleep, paralysis, etc.), their turn is skipped and the fight goes on by itself until they can act again. Companions first try to get the hero back on their feet, but finish off the enemy if that decides the fight right now.
        - The hero's death is a plot twist, not a silent ending.
        - Rank-and-file enemies have no ultimate skills, resurrection or legendary magic.

        ## Skills, mana and shield
        - Skills cost mana: tier 1 — 2–4, 2 — 4–7, 3 — 7–12, 4 — 12–18, 5 — 18–30. Mana does not regenerate by itself in combat.
        - Skill damage, healing and shields are measured in power dice K (1d8 at levels 1–4, 2d8 from 5th, 3d8 from 10th…): tier 1 — 1K, 2 — 1.5K, 3 — 2K, 4 — 3K, 5 — 4K to a single target. A skill's tier is no higher than the level allows (1 at 1–4, 2 at 5–8, 3 at 9–12, 4 at 13–16, 5 from 17th).
        - Skill categories: attack, support, heal, cleanse, shield, revive, debuff, utility. Targets: self / ally / enemy / all-allies / all-enemies.
        - Each character has no more than 8 skills. Strong skills are limited: "once per combat", an N-round cooldown or a rare consumable.
        - An energy shield is temporary HP on top of regular HP: damage removes the shield first. Shields don't stack (the larger value is kept) and don't exceed half of max HP.
        - Resurrection is extremely rare and expensive: a tier 4–5 skill, a rare consumable or a legendary item; one per combat, returning with 1 HP and the "Weakness" debuff for 2 rounds.

        ## Conditions and effects
        - Every buff and debuff has a duration in rounds; `combat_turn` ticks it at the end of the bearer's turn and removes expired ones, and applies damage over time and regeneration at the start of the turn. After combat, combat effects and shields wear off.
        - Morale: a badly wounded enemy, an enemy without a leader or a thinned-out group checks morale and may flee or surrender; undead, constructs and dungeon and arc bosses don't flee.
        - Hard control (stun, sleep, freeze, charm, fear, confusion, entangle, silence) — no longer than 1–2 rounds and with a repeated save; after such control the creature becomes immune to that type for 1 round.
        - Debuffs: weakness (−2 attack and damage), lowered defense (−2 AC), blindness, vulnerability (+50% damage taken), curse, slow.
        - Damage over time: burning (1d6 fire), bleeding (1d4–1d8), poison (1d4 poison and disadvantage on CON checks), acid, necrotic blight.
        - Support: blessing (+1d4 to attacks and saves), rage, haste, regeneration (+1d6 HP at the start of the turn), fortitude, evasion, invisibility, precision, mage armor (+3 AC), elemental barrier, cleansing.
        - Effects of the same type don't stack unless stated explicitly; reapplying refreshes the duration.

        ## Elements and weaknesses
        - Damage types: slashing, piercing, bludgeoning, fire, cold, lightning, poison, acid, necrotic, radiant, psychic, thunder, force.
        - Resistance — half damage, vulnerability — plus half, immunity — zero. Record a creature's weaknesses and resistances in Bestiary.md at the first encounter and keep them constant.

        ## Bosses
        - Elites and bosses have a poise bar (2–4 segments): hitting a weakness, a critical hit or strong control removes a segment; at zero — a "break" (a skipped turn and +50% damage taken for a round).
        - Dungeon and arc bosses get 1–3 legendary actions per round: an attack, a move or an effect.

        ## Magic
        - A spell costs mana (1–2 — simple, 3–5 — strong, 6+ — powerful).
        - The save DC against a spell: 8 + 2 + the caster's ability modifier.

        ## Rest
        - A short rest (≈1 hour, `rest short`): a quarter of HP and mana; no more than two between long rests.
        - A long rest (`rest long`): all HP and mana, "until rest" effects are removed, "once per day" properties are available again, morning comes. Outside a settlement a ration is eaten — without food only half is restored; in the wilds a night alarm is possible.

        ## Trade
        - Prices come from the merchant's shop (the trade window) and the reference prices in World.md.
        - A merchant gladly buys goods of their own trade (for about half the price), related goods cheaply, and doesn't take the rest.
        - The hero's Charisma gives a discount or markup of up to 15%; haggling is a CHA (Persuasion) check against DC 12–18.

        ## Experience and levels
        - XP for victories (an average fight ≈ 80, a hard one ≈ 120), quests (20–300) and resourcefulness (10–40). A new level every 600 XP; levels continue past the 20th (endgame).
        - A new level (level_up): HP and mana by the class archetype; proficiency, the power die K, combat experience (+1 AC at 6, 12, 18…) and the maximum skill tier grow by themselves.
        - The choice on level-up: a new skill or +1 skill tier; at 4, 8, 12, 16, 19 — also +2 to abilities.
        - Adversaries are built by level and role (plan_encounter); the danger of regions is given in World.md.

        ## Special rules of the world
        _Add rules that make this world special._
        """;

    public const string FactionsEn = """
        # Factions and powers

        > Template: one faction — one section. Write hidden goals and plans in Secrets.md.

        ## <Faction name>
        - **Who they are:** _…_
        - **Leader:** _…_
        - **Goals (open):** _…_
        - **Resources and influence:** _…_
        - **Attitude toward the hero:** _neutral_
        - **Allies / enemies:** _…_
        """;

    public const string BestiaryEn = """
        # Bestiary

        > Template: the creatures and typical adversaries of the world. Record a creature at the first encounter
        > so that its stats stay the same from fight to fight.

        ## <Creature>
        - **Type and habitat:** _…_
        - **CR / level:** _…_
        - **HP / AC:** _…_
        - **Attacks:** _+4 to hit, 1d6+2 slashing_
        - **Features and weaknesses:** _…_
        - **Behavior:** _…_
        - **Loot:** _what can be taken and sold._
        """;

    public const string SecretsEn = """
        # GM secrets

        > For the GM only. Don't retell this to the player directly — reveal it through events, clues and NPCs.

        ## The truth about the world
        _What is really going on._

        ## The main villain and their plan
        _Who, what they want, what they have already done, what they will do next._

        ## Hidden motives of NPCs
        _Name — what they hide._

        ## Upcoming twists
        _Surprises for the next scenes._

        ## Clues and where to find them
        _Clue — where it lies — which truth it leads to._

        ## Threat clocks
        _What happens if the hero does nothing: stage 1 → stage 2 → stage 3._
        """;

    public const string JournalEn = """
        # Campaign journal

        > The campaign's long-term memory: after each significant scene the GM appends an entry (edit_file append).
        > Entry format:
        > ### Day N — Place
        > - what happened; the hero's decisions; consequences and open threads.

        """;

    public const string CyberWorldEn = """
        # City: <name>

        > Campaign template. Fill in the sections, keeping the headings; delete what stays empty, add your own.

        ## In brief
        _2–3 sentences: what this city is and what makes it gripping._

        ## Flavor and tone
        - **Flavor:** _classic neon / corporate noir / post-cyberpunk / biopunk / …_
        - **Tone:** _noir / action / dark humor / drama…_
        - **Boundaries:** _what we avoid, how harsh it gets._

        ## Technology and the net
        - **Implants:** _how much of the body is replaced with chrome, the price and the risk of cyberpsychosis._
        - **The net:** _who controls it, how dangerous netrunning is, what lies beyond the Blackwall._
        - **Transport and weapons:** _AVs, bikes, smart weapons, drones._

        ## Districts
        _Districts and zones — one line each with a danger level: "Neon Market — lvl 1–3"._

        ## Corporations and power
        _Who rules the city; in detail — in Factions.md._

        ## Gangs and the street
        _Who holds the streets, their signs and territories._

        ## Economy and prices
        - **Currency:** _eddies (€$)._
        - **Reference prices:** _a capsule for the night 2 €$, noodles 1 €$, a pistol 15 €$, a kevlar vest 30 €$, a medstim 6 €$, a cab across the district 3 €$._
        - **Scarcity and plenty:** _what is expensive here and what is cheap._

        ## Law and the street
        _Police, private security, curfew, what you can get away with and what you can't._

        ## Culture
        _Fashion, music, braindances, religions and cults, holidays, taboos._

        ## History
        _3–5 past events that shape the present (corporate wars, disasters, the collapse of the net)._

        ## Rumors
        _What they gossip about in bars and on darknet forums — part truth, part lies._
        """;

    public const string CyberRulesEn = """
        # Game rules

        > Core rules in the spirit of D&D 5e in a cyberpunk setting. The GM extends them with the world's own rules (the "Special rules" section).

        ## Checks
        - **d20 + modifier** against the **Difficulty Class (DC)**; modifier = (ability − 10) / 2 rounded down.
        - DC: 5 — very easy, 10 — easy, 15 — medium, 20 — hard, 25 — very hard, 30 — nearly impossible.
        - Advantage / disadvantage: roll two d20, take the higher / lower.
        - Degrees of success (`skill_check`): 10 or more above the DC — brilliant success; meeting the DC — success; 1–3 below the DC — success at a cost (the goal is reached with a complication, the player may back off); lower — failure. A natural 20 raises the result one step, a natural 1 lowers it.

        ## Combat
        - Initiative: d20 + DEX, rolled once at the start of combat; `combat_turn` keeps track of the current participant and the round number.
        - The player's turn is their turn in initiative order: companions and adversaries act in the same turn until the round ends.
        - Attack: d20 + attack bonus against the target's AC. A natural 20 is a critical hit (damage dice are doubled), a natural 1 is a miss or a weapon jam.
        - Damage: the weapon die + modifier (STR — melee and blunt, DEX — firearms and light blades) + item and implant bonuses.
        - Saving throw: d20 + modifier against the effect's DC. Skill DC = 8 + proficiency + key ability modifier.
        - HP down to 0 — "dying": unconscious; death saves are made by `resolve_death_save`, which keeps the score (3 successes — stabilized, 3 failures — death, a natural 20 — comes to with 1 HP).
        - If the hero cannot act (0 HP, stun, short circuit, paralysis, etc.), their turn is skipped and the fight goes on by itself. Companions first try to get the hero back on their feet, but finish off the enemy if that decides the fight right now.
        - The hero's death is a plot twist, not a silent ending.
        - Rank-and-file enemies have no ultimate skills or resuscitation.

        ## Skills, RAM and shield
        - Skills (combat moves, hacking programs, implant protocols) cost RAM: tier 1 — 2–4, 2 — 4–7, 3 — 7–12, 4 — 12–18, 5 — 18–30. RAM does not regenerate by itself in combat.
        - Skill damage, healing and shields are measured in power dice K (1d8 at levels 1–4, 2d8 from 5th, 3d8 from 10th…): tier 1 — 1K, 2 — 1.5K, 3 — 2K, 4 — 3K, 5 — 4K to a single target. A skill's tier is no higher than the level allows (1 at 1–4, 2 at 5–8, 3 at 9–12, 4 at 13–16, 5 from 17th).
        - Skill categories: attack, support, heal, cleanse, shield, resuscitation, debuff, utility. Targets: self / ally / enemy / all-allies / all-enemies.
        - Each character has no more than 8 skills. Strong skills are limited: "once per combat", an N-round cooldown or a rare consumable.
        - A force field is temporary HP on top of regular HP: damage removes the shield first. Shields don't stack (the larger value is kept) and don't exceed half of max HP.
        - Resuscitation is extremely rare and expensive: a tier 4–5 skill, a resuscitation kit or an iconic item; one per combat, returning with 1 HP and the "Weakness" debuff for 2 rounds.

        ## Conditions and effects
        - Every buff and debuff has a duration in rounds; `combat_turn` ticks it at the end of the bearer's turn and removes expired ones, and applies damage over time and regeneration at the start of the turn. After combat, combat effects and shields wear off.
        - Morale: a badly wounded enemy, an enemy without a leader or a thinned-out group checks morale and may flee or surrender; undead, constructs and dungeon and arc bosses don't flee.
        - Hard control (stun, short circuit, neural hack, paralysis, panic, navigation glitch, grapple, implant lockout) — no longer than 1–2 rounds and with a repeated save; afterwards the target becomes immune to that type for 1 round.
        - Debuffs: suppressing fire (−2 attack), pierced armor (−2 AC), optics glitch, target mark (+50% damage taken), virus, overload.
        - Damage over time: burning (1d6 thermal), bleeding (1d4–1d8), toxin (1d4 chemical and disadvantage on CON checks), corrosion, radiation.
        - Support: tactical sync (+1d4 to attacks and saves), combat stim, reflex booster, nano-regeneration (+1d6 HP at the start of the turn), armored skin, evasion, optical camo, targeting, force screen (+3 AC), insulation from a damage type, reboot.
        - Effects of the same type don't stack unless stated explicitly; reapplying refreshes the duration.

        ## Damage types and vulnerabilities
        - Damage types: kinetic, slashing, blunt, thermal, cryo, EMP, chemical, acid, radiation, cyberattack, sonic, energy.
        - Resistance — half damage, vulnerability — plus half, immunity — zero. Record an adversary's vulnerabilities in Bestiary.md at the first encounter and keep them constant.
        - Guidelines: drones, mechs and cyborgs are vulnerable to EMP; programs and AIs — to cyberattack and immune to physical damage outside the net; mutants resist toxins and radiation.

        ## Bosses
        - Elites and bosses have a poise bar (2–4 segments): hitting a vulnerability, a critical hit or strong control removes a segment; at zero — a "break" (a skipped turn and +50% damage taken for a round).
        - Location and arc bosses get 1–3 special actions per round: a burst, reinforcements, a turret, a hack.

        ## Netrunning
        - A hacking program costs RAM (1–2 — simple, 3–5 — strong, 6+ — powerful).
        - The save DC against a program: 8 + 2 + the netrunner's INT modifier; the target defends with INT.
        - Hacking scene objects (doors, cameras, turrets) is an INT check; a failure may raise the alarm.

        ## Rest
        - A short rest (≈1 hour, `rest short`): a quarter of HP and RAM; no more than two between long rests.
        - A long rest (`rest long`): all HP and RAM, "until rest" effects are removed, "once per day" properties are available again, morning comes. Outside a settlement a ration is eaten — without food only half is restored; in the wilds a night alarm is possible.

        ## Trade
        - Prices come from the merchant's shop (the trade window) and the reference prices in World.md.
        - A merchant gladly buys goods of their own line (for about half the price), related goods cheaply, and doesn't take the rest.
        - The hero's Charisma gives a discount or markup of up to 15%; haggling is a CHA (Persuasion) check against DC 12–18.

        ## Experience and levels
        - XP for victories (an average fight ≈ 80, a hard one ≈ 120), gigs (20–300) and resourcefulness (10–40). A new level every 600 XP; levels continue past the 20th (endgame).
        - A new level (level_up): HP and RAM by the class archetype; proficiency, the power die K, combat experience (+1 AC at 6, 12, 18…) and the maximum skill tier grow by themselves.
        - The choice on level-up: a new skill or +1 skill tier; at 4, 8, 12, 16, 19 — also +2 to abilities.
        - Adversaries are built by level and role (plan_encounter); the danger of districts is given in World.md.

        ## Special rules of the world
        _Add rules that make this city special._
        """;

    public const string CyberBestiaryEn = """
        # Adversary dossiers

        > Template: the city's typical adversaries. Record an adversary at the first encounter
        > so that its stats stay the same from fight to fight.

        ## <Adversary>
        - **Type and where encountered:** _human / cyborg / android / drone / mech / program / mutant; district._
        - **Level:** _…_
        - **HP / AC:** _…_
        - **Attacks:** _+4 to hit, 1d6+2 kinetic_
        - **Vulnerabilities and resistances:** _…_
        - **Behavior:** _…_
        - **Loot:** _what can be taken and sold._
        """;

    public const string ModernWorldEn = """
        # World: <city and time>

        > Campaign template. Fill in the sections, keeping the headings; delete what stays empty, add your own.

        ## In brief
        _2–3 sentences: where and when the story takes place and what makes it gripping._

        ## Place, time, genre
        - **Place:** _a country, a city (real or fictional), its surroundings._
        - **Time:** _present day / the nineties / the recent past._
        - **Genre and tone:** _crime drama / action / detective / mystical thriller / urban fantasy; realism / noir / horror / humorous._
        - **The supernatural:** _none / rare secret magic / a hidden magical society / open magic; who knows and how the world reacts._
        - **Boundaries:** _what we avoid, how harsh it gets._

        ## Districts and surroundings
        _Districts, suburbs and places outside the city — one line each with a danger level: "Bedroom district — lvl 1–3"._

        ## Authorities and law enforcement
        _Police, security services, officials, courts — who is honest, who can be bought and for how much._

        ## Crime
        _Gangs, crews, cartels — who controls what; in detail — in Factions.md._

        ## Economy and prices
        - **Currency:** _dollars / euros / local currency (shown as $ in the interface)._
        - **Reference prices:** _a meal 1 $, a night in a motel 3 $, a cab across the city 2 $, a pistol 15 $, body armor 40 $, a first aid kit 25 $, a used car 400 $._
        - **Black market prices:** _weapons, documents, information._

        ## Culture and everyday life
        _How people live, music, habits, holidays, what is considered normal and what isn't._

        ## History
        _3–5 past events that shape the present (turf wars, high-profile murders, crises, wars)._

        ## Rumors
        _What they gossip about in bars and around the neighborhood — part truth, part lies._
        """;

    public const string ModernRulesEn = """
        # Game rules

        > Core rules in the spirit of D&D 5e in a modern setting — from realism to urban fantasy. The GM extends them with the world's own rules (the "Special rules" section).

        ## Checks
        - **d20 + modifier** against the **Difficulty Class (DC)**; modifier = (ability − 10) / 2 rounded down.
        - DC: 5 — very easy, 10 — easy, 15 — medium, 20 — hard, 25 — very hard, 30 — nearly impossible.
        - Advantage / disadvantage: roll two d20, take the higher / lower.
        - Degrees of success (`skill_check`): 10 or more above the DC — brilliant success; meeting the DC — success; 1–3 below the DC — success at a cost (the goal is reached with a complication, the player may back off); lower — failure. A natural 20 raises the result one step, a natural 1 lowers it.

        ## Combat
        - Initiative: d20 + DEX, rolled once at the start of combat; `combat_turn` keeps track of the current participant and the round number.
        - The player's turn is their turn in initiative order: companions and adversaries act in the same turn until the round ends.
        - Attack: d20 + attack bonus against the target's AC. A natural 20 is a critical hit (damage dice are doubled), a natural 1 is a miss or a misfire.
        - Damage: the weapon die + modifier (STR — hand-to-hand and melee weapons, DEX — firearms and knives) + item bonuses.
        - Saving throw: d20 + modifier against the effect's DC. Skill DC = 8 + proficiency + key ability modifier.
        - HP down to 0 — "dying": unconscious; death saves are made by `resolve_death_save`, which keeps the score (3 successes — stabilized, 3 failures — death, a natural 20 — comes to with 1 HP).
        - If the hero cannot act (0 HP, stun, concussion, electric shock, etc.), their turn is skipped and the fight goes on by itself. Companions first try to pull the hero out, but end the fight if that decides it right now.
        - The hero's death is a plot twist, not a silent ending.
        - Rank-and-file enemies have no ultimate moves; bandits retreat and surrender when things go badly.
        - Cover: +2 AC, good cover (concrete, a car's engine block) — +5.

        ## Skills, focus and shield
        - Special moves and spells cost focus: tier 1 — 2–4, 2 — 4–7, 3 — 7–12, 4 — 12–18, 5 — 18–30. Focus does not regenerate by itself in combat.
        - Skill damage, healing and shields are measured in power dice K (1d8 at levels 1–4, 2d8 from 5th, 3d8 from 10th…): tier 1 — 1K, 2 — 1.5K, 3 — 2K, 4 — 3K, 5 — 4K to a single target. A skill's tier is no higher than the level allows (1 at 1–4, 2 at 5–8, 3 at 9–12, 4 at 13–16, 5 from 17th).
        - Skill categories: attack, support, heal, effect removal, shield, back on their feet, debuff, utility. Targets: self / ally / enemy / all-allies / all-enemies.
        - Each character has no more than 8 skills. Strong moves are limited: "once per combat", an N-round cooldown or a consumable.
        - A shield is temporary HP on top of regular HP (an armor plate, a riot shield): damage removes the shield first. Shields don't stack and don't exceed half of max HP.
        - Getting back on their feet is extremely rare: a tier 4–5 skill, adrenaline from an army first aid kit or a legendary item; once per combat, returning with 1 HP and the "Weakness" debuff for 2 rounds.

        ## Conditions and effects
        - Every buff and debuff has a duration in rounds; `combat_turn` ticks it at the end of the bearer's turn and removes expired ones, and applies damage over time and regeneration at the start of the turn. After combat, combat effects and shields wear off.
        - Morale: a badly wounded enemy, an enemy without a leader or a thinned-out group checks morale and may flee or surrender; undead, constructs and dungeon and arc bosses don't flee.
        - Hard control (stun, concussion, electric shock, panic, grapple and handcuffs, flash blindness) — no longer than 1–2 rounds and with a repeated save; afterwards the target becomes immune to that type for 1 round.
        - Debuffs: suppressing fire (−2 attack), pierced armor (−2 AC), a fracture, target mark (+50% damage taken), dizziness, tear gas.
        - Damage over time: bleeding (1d4–1d8, removed by bandaging), burning (1d6 fire), gas poisoning (1d4 chemical).
        - Support: tactical coordination (+1d4 to attacks and saves), adrenaline, composure, cover (+2 AC), aiming, armor plate (+3 AC), second wind (+1d6 HP at the start of the turn).
        - Effects of the same type don't stack unless stated explicitly; reapplying refreshes the duration.

        ## Damage types and vulnerabilities
        - Damage types: ballistic, slashing, piercing, blunt, fire, cold, explosive, electric, chemical; in a magical world also necrotic, radiant and psychic.
        - Resistance — half damage, vulnerability — plus half, immunity — zero. Record an adversary's vulnerabilities in Bestiary.md at the first encounter.
        - Guidelines: body armor gives resistance to ballistic damage to the torso; cars and armored vehicles are vulnerable to explosives and armor-piercing rounds; animals fear fire and explosions.

        ## Bosses
        - Elites and bosses have a poise bar (2–4 segments): hitting a vulnerability, a critical hit or strong control removes a segment; at zero — a "break" (a skipped turn and +50% damage taken for a round).
        - Site and arc bosses get 1–3 special actions per round: a burst, an order to henchmen, a grenade, falling back behind cover.

        ## Wounds and consequences
        - Serious wounds are treated in a hospital or by "your own" doctor; gunshot wounds in a hospital raise questions from the police.
        - Loud shooting in the city attracts the police; killings — a manhunt. The GM plays out the consequences honestly.

        ## Rest
        - A short rest (≈1 hour, `rest short`): a quarter of HP and focus; no more than two between long rests.
        - A long rest (`rest long`): all HP and focus, "until rest" effects are removed, "once per day" properties are available again, morning comes. Outside a settlement a ration is eaten — without food only half is restored; in the wilds a night alarm is possible.

        ## Trade
        - Prices come from the merchant's store (the trade window) and the reference prices in World.md.
        - A merchant gladly buys goods of their own line (for about half the price), related goods cheaply, and doesn't take the rest.
        - The hero's Charisma gives a discount or markup of up to 15%; haggling is a CHA (Persuasion) check against DC 12–18.

        ## Experience and levels
        - XP for victories (an average fight ≈ 80, a hard one ≈ 120), jobs (20–300) and resourcefulness (10–40). A new level every 600 XP; levels continue past the 20th (endgame).
        - A new level (level_up): HP and focus by the archetype; proficiency, the power die K, combat experience (+1 AC at 6, 12, 18…) and the maximum skill tier grow by themselves.
        - The choice on level-up: a new skill or +1 skill tier; at 4, 8, 12, 16, 19 — also +2 to abilities.
        - Adversaries are built by level and role (plan_encounter); the danger of districts is given in World.md.

        ## Special rules of the world
        _Add rules that make this world special._
        """;

    public const string ModernBestiaryEn = """
        # Adversary dossiers

        > Template: the world's typical adversaries. Record an adversary at the first encounter
        > so that its stats stay the same from fight to fight.

        ## <Adversary>
        - **Who and where encountered:** _bandit / guard / dog / car / spirit / vampire / anomaly; district._
        - **Level:** _…_
        - **HP / AC:** _…_
        - **Attacks:** _+4 to hit, 1d6+2 ballistic_
        - **Vulnerabilities and resistances:** _…_
        - **Behavior:** _…_
        - **Loot:** _what can be taken and sold._
        """;

    private static readonly (string File, string Content)[] FantasyFilesEn =
    {
        ("World.md", WorldEn),
        ("Rules.md", RulesEn),
        ("Factions.md", FactionsEn),
        ("Bestiary.md", BestiaryEn),
        ("Secrets.md", SecretsEn),
        ("Journal.md", JournalEn),
    };

    private static readonly (string File, string Content)[] CyberFilesEn =
    {
        ("World.md", CyberWorldEn),
        ("Rules.md", CyberRulesEn),
        ("Factions.md", FactionsEn),
        ("Bestiary.md", CyberBestiaryEn),
        ("Secrets.md", SecretsEn),
        ("Journal.md", JournalEn),
    };

    private static readonly (string File, string Content)[] ModernFilesEn =
    {
        ("World.md", ModernWorldEn),
        ("Rules.md", ModernRulesEn),
        ("Factions.md", FactionsEn),
        ("Bestiary.md", ModernBestiaryEn),
        ("Secrets.md", SecretsEn),
        ("Journal.md", JournalEn),
    };
}
