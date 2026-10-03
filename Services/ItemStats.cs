namespace RPG_Harness.Services;

/// <summary>
/// Характеристики предметов: базовый урон и броня по каталогу, бюджет бонусов по редкости,
/// разбор параметров инструментов и итоговые показатели героя с учётом надетого.
/// </summary>
public static class ItemStats
{
    /// <summary>Ключи числовых бонусов в порядке показа.</summary>
    public static readonly string[] Keys = { "str", "dex", "con", "int", "wis", "cha", "attack", "damage", "ac", "hp", "mana" };

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["str"] = "str", ["сил"] = "str", ["сила"] = "str",
        ["dex"] = "dex", ["лов"] = "dex", ["ловкость"] = "dex",
        ["con"] = "con", ["тел"] = "con", ["телосложение"] = "con",
        ["int"] = "int", ["инт"] = "int", ["интеллект"] = "int",
        ["wis"] = "wis", ["мдр"] = "wis", ["мудрость"] = "wis",
        ["cha"] = "cha", ["хар"] = "cha", ["харизма"] = "cha",
        ["attack"] = "attack", ["atk"] = "attack", ["hit"] = "attack", ["атака"] = "attack", ["попадание"] = "attack",
        ["damage"] = "damage", ["dmg"] = "damage", ["урон"] = "damage",
        ["ac"] = "ac", ["armor"] = "ac", ["кб"] = "ac", ["кд"] = "ac", ["защита"] = "ac",
        ["hp"] = "hp", ["хп"] = "hp", ["здоровье"] = "hp", ["health"] = "hp",
        ["mana"] = "mana", ["mp"] = "mana", ["мана"] = "mana", ["ram"] = "mana", ["focus"] = "mana",
        ["strength"] = "str", ["dexterity"] = "dex", ["constitution"] = "con", ["intelligence"] = "int", ["wisdom"] = "wis", ["charisma"] = "cha",
        ["defense"] = "ac",
    };

    public static string KeyTitle(string key) => Lang.IsEn ? key switch
    {
        "str" => "STR", "dex" => "DEX", "con" => "CON", "int" => "INT", "wis" => "WIS", "cha" => "CHA",
        "attack" => "attack", "damage" => "damage", "ac" => "AC", "hp" => "HP", "mana" => Genre.Mana,
        _ => key,
    } : key switch
    {
        "str" => "СИЛ", "dex" => "ЛОВ", "con" => "ТЕЛ", "int" => "ИНТ", "wis" => "МДР", "cha" => "ХАР",
        "attack" => "атака", "damage" => "урон", "ac" => "КБ", "hp" => "HP", "mana" => "мана",
        _ => key,
    };

    // ===== Баланс =====

    /// <summary>Очки бонусов по редкости: +1 к характеристике/атаке/урону = 1, +1 КБ = 2, +5 HP или маны = 1.</summary>
    public static int Budget(string? rarity) => ItemEconomy.RarityKey(rarity) switch
    {
        "uncommon" => 1,
        "rare" => 2,
        "epic" => 4,
        "legendary" => 6,
        _ => 0,
    };

    /// <summary>
    /// Бюджет с учётом уровня предмета: +10% за каждый уровень выше первого (редкая вещь 11-го уровня — 4 очка).
    /// Так добыча эндгейма остаётся желанной, а точность (атака, КБ) не разгоняется — у них постоянный потолок.
    /// </summary>
    public static int Budget(string? rarity, int level) =>
        (int)Math.Floor(Budget(rarity) * (1 + (Math.Max(1, level) - 1) / 10.0));

    /// <summary>Сколько особых свойств (эффектов) может быть у предмета.</summary>
    public static int EffectLimit(string? rarity) => ItemEconomy.RarityKey(rarity) switch
    {
        "uncommon" => 1,
        "rare" => 1,
        "epic" => 2,
        "legendary" => 3,
        _ => 0,
    };

    /// <summary>Потолок одного бонуса: атака и КБ всегда ±3 (ограниченная точность), остальное растёт с уровнем предмета.</summary>
    private static int Cap(string key, int level = 1)
    {
        var l = Math.Max(1, level) - 1;
        return key switch
        {
            "hp" or "mana" => 30 + 3 * l,
            "attack" or "ac" => 3,
            "damage" => 3 + l / 5,
            _ => 3 + l / 10,
        };
    }

    /// <summary>Цена одного шага бонуса в очках бюджета и размер шага.</summary>
    private static (int Cost, int Step) StepOf(string key) => key switch
    {
        "ac" => (2, 1),
        "hp" or "mana" => (1, 5),
        _ => (1, 1),
    };

    private static int CostOf(string key, int value)
    {
        if (value <= 0)
        {
            return 0;
        }

        var (cost, step) = StepOf(key);
        return (value + step - 1) / step * cost;
    }

    /// <summary>Штрафы (проклятия, тяжесть) возвращают очки, но не больше двух.</summary>
    private static int Refund(Dictionary<string, int> bonuses) =>
        Math.Min(2, bonuses.Where(b => b.Value < 0).Sum(b => (-b.Value + StepOf(b.Key).Step - 1) / StepOf(b.Key).Step));

    public static int Spent(Dictionary<string, int> bonuses) => bonuses.Sum(b => CostOf(b.Key, b.Value));

    /// <summary>Максимальная броня предмета по слоту (доспех до 8, щит до 3, шлем/перчатки/сапоги/плащ/пояс до 1).</summary>
    public static int ArmorCap(EquipSlot? slot) => slot switch
    {
        EquipSlot.Body => 8,
        EquipSlot.Hand2 => 3,
        EquipSlot.Helmet or EquipSlot.Gloves or EquipSlot.Boots or EquipSlot.Cloak or EquipSlot.Belt => 1,
        null => 8,
        _ => 0,
    };

    /// <summary>
    /// Приводит предмет к бюджету редкости: режет лишние бонусы, эффекты и броню.
    /// Возвращает предупреждения для результата инструмента (пусто — всё в норме).
    /// </summary>
    public static List<string> Balance(GridItem item)
    {
        var warn = new List<string>();
        var rarity = ItemEconomy.RarityKey(item.Rarity);

        var armorCap = ArmorCap(item.Slot);
        if (item.Armor > armorCap || item.Armor < 0)
        {
            var was = item.Armor;
            item.Armor = Math.Clamp(item.Armor, 0, armorCap);
            warn.Add(Lang.T($"броня {was} → {item.Armor} (потолок для этого слота {armorCap}; зачарование брони — бонусом ac)", $"armor {was} → {item.Armor} (the cap for this slot is {armorCap}; armor enchantment goes as an ac bonus)"));
        }

        foreach (var key in item.Bonuses.Keys.ToList())
        {
            var cap = Cap(key, item.Level);
            var v = item.Bonuses[key];
            if (v == 0)
            {
                item.Bonuses.Remove(key);
            }
            else if (Math.Abs(v) > cap)
            {
                item.Bonuses[key] = Math.Sign(v) * cap;
                warn.Add($"{KeyTitle(key)} {v:+#;-#} → {item.Bonuses[key]:+#;-#} ({Lang.T("потолок", "cap")} ±{cap})");
            }
        }

        var budget = Budget(rarity, item.Level) + Refund(item.Bonuses);
        var cut = new List<string>();
        while (Spent(item.Bonuses) > budget)
        {
            // Срезаем с конца списка: первые названные бонусы — главная задумка вещи.
            var key = item.Bonuses.Last(b => b.Value > 0).Key;
            var step = StepOf(key).Step;
            var nv = Math.Max(0, item.Bonuses[key] - step);
            if (nv == 0)
            {
                item.Bonuses.Remove(key);
            }
            else
            {
                item.Bonuses[key] = nv;
            }

            cut.Add(KeyTitle(key));
        }

        if (cut.Count > 0)
        {
            warn.Add(Lang.T($"бонусы урезаны до бюджета редкости «{ItemEconomy.RarityTitle(rarity)}» ур. {Math.Max(1, item.Level)} ({Budget(rarity, item.Level)} очк.): " +
                     $"снижено {string.Join(", ", cut.Distinct())}",
                     $"bonuses trimmed to the budget of rarity \"{ItemEconomy.RarityTitle(rarity)}\" lvl {Math.Max(1, item.Level)} ({Budget(rarity, item.Level)} pts.): " +
                     $"reduced {string.Join(", ", cut.Distinct())}"));
        }

        var limit = EffectLimit(rarity);
        item.Effects = item.Effects.Select(e => e.Trim()).Where(e => e.Length > 0).Distinct().ToList();
        if (item.Effects.Count > limit)
        {
            var dropped = item.Effects.Skip(limit).ToList();
            item.Effects = item.Effects.Take(limit).ToList();
            warn.Add(limit == 0
                ? Lang.T($"у обычных вещей нет особых свойств — убрано: {string.Join("; ", dropped)} (опиши это в note как особенность без механики или повысь редкость)", $"common items have no special properties — removed: {string.Join("; ", dropped)} (describe it in note as a feature without mechanics or raise the rarity)")
                : Lang.T($"свойств больше лимита редкости ({limit}) — убрано: {string.Join("; ", dropped)}", $"more properties than the rarity limit ({limit}) — removed: {string.Join("; ", dropped)}"));
        }

        return warn;
    }

    // ===== Базовые значения каталога =====

    private static readonly Dictionary<string, (string Damage, int Armor)> BaseTable = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dagger"] = ("1d4 колющий", 0), ["athame"] = ("1d4 колющий", 0), ["short_sword"] = ("1d6 колющий", 0),
        ["sword"] = ("1d8 рубящий", 0), ["long_sword"] = ("1d8 рубящий", 0), ["rapier"] = ("1d8 колющий", 0),
        ["scimitar"] = ("1d6 рубящий", 0), ["falchion"] = ("1d8 рубящий", 0), ["hand_axe"] = ("1d6 рубящий", 0),
        ["axe"] = ("1d8 рубящий", 0), ["war_axe"] = ("1d8 рубящий", 0), ["club"] = ("1d4 дробящий", 0),
        ["mace"] = ("1d6 дробящий", 0), ["morningstar"] = ("1d8 колющий", 0), ["flail"] = ("1d8 дробящий", 0),
        ["hammer"] = ("1d8 дробящий", 0), ["whip"] = ("1d4 рубящий", 0), ["demon_blade"] = ("1d8 рубящий", 0),
        ["wand"] = ("1d4 дробящий", 0), ["rod"] = ("1d6 дробящий", 0),
        ["greatsword"] = ("2d6 рубящий", 0), ["battle_axe"] = ("1d12 рубящий", 0), ["executioner_axe"] = ("1d12 рубящий", 0),
        ["great_mace"] = ("2d6 дробящий", 0), ["staff"] = ("1d6 дробящий", 0), ["quarterstaff"] = ("1d6 дробящий", 0),
        ["mage_staff"] = ("1d6 дробящий", 0), ["elder_staff"] = ("1d6 дробящий", 0), ["spear"] = ("1d8 колющий", 0),
        ["trident"] = ("1d8 колющий", 0), ["halberd"] = ("1d10 рубящий", 0), ["glaive"] = ("1d10 рубящий", 0),
        ["scythe"] = ("2d4 рубящий", 0), ["bow"] = ("1d6 колющий", 0), ["shortbow"] = ("1d6 колющий", 0),
        ["longbow"] = ("1d8 колющий", 0), ["crossbow"] = ("1d10 колющий", 0), ["sling"] = ("1d4 дробящий", 0),
        ["hand_cannon"] = ("1d12 колющий", 0),
        ["buckler"] = ("", 1), ["shield"] = ("", 2), ["kite_shield"] = ("", 2), ["tower_shield"] = ("", 3),
        ["leather_armour"] = ("", 1), ["animal_skin"] = ("", 2), ["ring_mail"] = ("", 4), ["scale_mail"] = ("", 4),
        ["chain_mail"] = ("", 6), ["plate"] = ("", 8), ["dragon_armour"] = ("", 6),
        ["helmet"] = ("", 1), ["great_helm"] = ("", 1), ["horned_helm"] = ("", 1), ["gauntlets"] = ("", 1),
        // Расширение каталога: артефакты, драконьи доспехи, посохи и жезлы.
        ["sword_singing"] = ("1d8 рубящий", 0), ["sword_power"] = ("1d8 рубящий", 0), ["katana"] = ("1d8 рубящий", 0),
        ["sword_blood"] = ("1d8 рубящий", 0), ["sword_flame"] = ("1d8 рубящий", 0), ["sword_arc"] = ("1d8 рубящий", 0),
        ["dagger_vampire"] = ("1d4 колющий", 0), ["dagger_precise"] = ("1d4 колющий", 0), ["cutlass"] = ("1d6 рубящий", 0),
        ["scimitar_bloom"] = ("1d6 рубящий", 0), ["sword_leech"] = ("1d8 рубящий", 0), ["sabre_crimson"] = ("1d6 рубящий", 0),
        ["double_sword"] = ("1d8 рубящий", 0), ["quickblade"] = ("1d6 колющий", 0), ["triple_sword"] = ("2d6 рубящий", 0),
        ["blessed_blade"] = ("1d8 рубящий", 0), ["long_sword_steel"] = ("1d8 рубящий", 0), ["dagger_steel"] = ("1d4 колющий", 0),
        ["axe_frost"] = ("1d8 рубящий", 0), ["mace_holy"] = ("1d8 дробящий", 0), ["maul_skull"] = ("2d6 дробящий", 0),
        ["axe_holy"] = ("1d12 рубящий", 0), ["axe_demon"] = ("1d12 рубящий", 0), ["maul_dark"] = ("2d6 дробящий", 0),
        ["sceptre_torment"] = ("1d6 дробящий", 0), ["broad_axe"] = ("1d8 рубящий", 0), ["dire_flail"] = ("2d6 дробящий", 0),
        ["eveningstar"] = ("1d8 колющий", 0), ["giant_club"] = ("1d12 дробящий", 0), ["spiked_club"] = ("2d6 дробящий", 0),
        ["demon_whip"] = ("1d6 рубящий", 0), ["sacred_scourge"] = ("1d6 рубящий", 0), ["mace_flanged"] = ("1d6 дробящий", 0),
        ["whip_spell"] = ("1d4 рубящий", 0), ["lance_wyrm"] = ("1d10 колющий", 0), ["spear_crystal"] = ("1d10 колющий", 0),
        ["scythe_curse"] = ("2d4 рубящий", 0), ["lance_order"] = ("1d10 колющий", 0), ["lance_force"] = ("1d10 колющий", 0),
        ["bardiche"] = ("1d10 рубящий", 0), ["lajatang"] = ("1d8 рубящий", 0), ["partisan"] = ("1d10 колющий", 0),
        ["demon_trident"] = ("1d10 колющий", 0), ["trishula"] = ("1d10 колющий", 0), ["bow_storm"] = ("1d8 колющий", 0),
        ["crossbow_sniper"] = ("1d10 колющий", 0), ["orcbow"] = ("1d8 колющий", 0), ["blowgun"] = ("1d4 колющий", 0),
        ["triple_crossbow"] = ("1d8 колющий", 0), ["staff_venom"] = ("1d6 дробящий", 0), ["staff_elements"] = ("1d6 дробящий", 0),
        ["staff_fire"] = ("1d6 дробящий", 0), ["wand_winter"] = ("1d4 дробящий", 0), ["staff_battle"] = ("1d6 дробящий", 0),
        ["staff_crook"] = ("1d6 дробящий", 0), ["staff_bone"] = ("1d6 дробящий", 0), ["staff_gold"] = ("1d6 дробящий", 0),
        ["staff_blood"] = ("1d6 дробящий", 0), ["staff_sun"] = ("1d6 дробящий", 0), ["staff_ember"] = ("1d6 дробящий", 0),
        ["staff_orb"] = ("1d6 дробящий", 0), ["staff_spiral"] = ("1d6 дробящий", 0), ["staff_arcane"] = ("1d6 дробящий", 0),
        ["wand_bone"] = ("1d4 дробящий", 0), ["wand_fire"] = ("1d4 дробящий", 0), ["wand_void"] = ("1d4 дробящий", 0),
        ["wand_frost"] = ("1d4 дробящий", 0), ["wand_storm"] = ("1d4 дробящий", 0), ["wand_charm"] = ("1d4 дробящий", 0),
        ["rod_blood"] = ("1d6 дробящий", 0), ["rod_royal"] = ("1d6 дробящий", 0), ["rod_shadow"] = ("1d6 дробящий", 0),
        ["shield_storm"] = ("", 2), ["shield_gong"] = ("", 2), ["shield_heraldic"] = ("", 2), ["tower_shield_dark"] = ("", 3),
        ["buckler_bronze"] = ("", 1), ["mail_salamander"] = ("", 6), ["plate_orange"] = ("", 8), ["armor_justicar"] = ("", 8),
        ["plate_crystal"] = ("", 8), ["plate_dark"] = ("", 8), ["brigandine"] = ("", 5), ["scale_dragonking"] = ("", 6),
        ["armor_bone"] = ("", 5), ["dragon_storm_armour"] = ("", 6), ["dragon_ice_armour"] = ("", 6), ["dragon_shadow_armour"] = ("", 6),
        ["dragon_gold_armour"] = ("", 6), ["dragon_acid_armour"] = ("", 6), ["dragon_swamp_armour"] = ("", 6), ["dragon_pearl_armour"] = ("", 6),
        ["troll_hide"] = ("", 3), ["robe_vines"] = ("", 1), ["helm_dragon"] = ("", 1), ["gauntlets_war"] = ("", 1),
        ["helm_ornate"] = ("", 1), ["helm_plumed"] = ("", 1), ["helm_barbute"] = ("", 1), ["gauntlets_ornate"] = ("", 1),
    };

    /// <summary>Стрелковое оружие — атака и урон от ЛОВ.</summary>
    private static readonly HashSet<string> Ranged = new(StringComparer.OrdinalIgnoreCase)
        { "bow", "shortbow", "longbow", "crossbow", "sling", "hand_cannon", "bow_storm", "crossbow_sniper", "orcbow", "blowgun", "triple_crossbow" };

    /// <summary>Фехтовальное оружие — лучшая из СИЛ и ЛОВ.</summary>
    private static readonly HashSet<string> Finesse = new(StringComparer.OrdinalIgnoreCase)
        { "dagger", "athame", "short_sword", "rapier", "scimitar", "whip", "dagger_vampire", "dagger_precise", "cutlass", "scimitar_bloom",
          "sabre_crimson", "quickblade", "dagger_steel", "demon_whip", "sacred_scourge", "whip_spell", "katana" };

    /// <summary>Тяжёлые доспехи — ЛОВ к КБ не прибавляется; средние — не больше +2.</summary>
    private static readonly HashSet<string> Heavy = new(StringComparer.OrdinalIgnoreCase)
        { "ring_mail", "chain_mail", "plate", "mail_salamander", "plate_orange", "armor_justicar", "plate_crystal", "plate_dark" };
    private static readonly HashSet<string> Medium = new(StringComparer.OrdinalIgnoreCase)
        { "animal_skin", "scale_mail", "dragon_armour", "brigandine", "scale_dragonking", "armor_bone", "troll_hide", "dragon_storm_armour",
          "dragon_ice_armour", "dragon_shadow_armour", "dragon_gold_armour", "dragon_acid_armour", "dragon_swamp_armour", "dragon_pearl_armour" };

    public static (string Damage, int Armor) Base(string? icon) =>
        icon is not null && (BaseTable.TryGetValue(icon, out var b) || Registered.TryGetValue(icon, out b)) ? b : ("", 0);

    /// <summary>Урон и броня вещей, у которых механика записана в самом каталоге (киберпанк).</summary>
    private static readonly Dictionary<string, (string Damage, int Armor)> Registered = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Подхватывает механику из каталога: урон, броню, стрелковое/фехтовальное, вес доспеха.
    /// Вызывается ItemCatalog при загрузке — так киберпанковый каталог не дублируется таблицами в коде.
    /// </summary>
    public static void Register(IEnumerable<ItemDef> defs)
    {
        lock (Registered)
        {
            foreach (var d in defs)
            {
                if (d.Damage is { Length: > 0 } || d.Armor > 0)
                {
                    Registered[d.Id] = (d.Damage ?? "", d.Armor);
                }

                if (d.Ranged) Ranged.Add(d.Id);
                if (d.Finesse) Finesse.Add(d.Id);
                if (d.Weight == "heavy") Heavy.Add(d.Id);
                if (d.Weight == "medium") Medium.Add(d.Id);
            }
        }
    }

    /// <summary>Стрелковое оружие (атака от ЛОВ).</summary>
    public static bool IsRanged(string? icon) => icon is not null && Ranged.Contains(icon);

    /// <summary>Заполняет пустой урон и броню значениями каталога.</summary>
    public static void ApplyBase(GridItem item)
    {
        var (dmg, armor) = Base(item.Icon);
        if (item.Damage.Length == 0 && dmg.Length > 0 && item.Slot is EquipSlot.Hand1 or EquipSlot.Hand2 or null)
        {
            // Типы урона в таблицах записаны по-русски; английский интерфейс получает английские слова.
            item.Damage = DamageWords.Localize(dmg);
        }

        if (item.Armor == 0 && armor > 0)
        {
            item.Armor = armor;
        }
    }

    /// <summary>Случайные бонусы в пределах бюджета редкости — для товаров, созданных генератором лавок.</summary>
    public static void RollBonuses(GridItem item, Random rng)
    {
        var budget = Budget(item.Rarity, item.Level);
        if (budget == 0 || item.Slot is null)
        {
            return;
        }

        string[] pool = item.Slot switch
        {
            EquipSlot.Hand1 when item.Damage.Length > 0 => new[] { "attack", "damage", "attack", "damage", "str", "dex" },
            EquipSlot.Hand2 or EquipSlot.Body or EquipSlot.Helmet => new[] { "ac", "con", "hp", "str", "wis" },
            EquipSlot.Amulet or EquipSlot.Ring1 or EquipSlot.Ring2 => new[] { "int", "wis", "cha", "mana", "hp", "con", "dex" },
            _ => new[] { "dex", "con", "str", "hp", "ac" },
        };

        for (var guard = 0; guard < 20 && Spent(item.Bonuses) < budget; guard++)
        {
            var key = pool[rng.Next(pool.Length)];
            var (cost, step) = StepOf(key);
            var next = item.Bonuses.GetValueOrDefault(key) + step;
            if (Spent(item.Bonuses) + cost > budget || next > Cap(key, item.Level))
            {
                continue;
            }

            item.Bonuses[key] = next;
        }
    }

    /// <summary>Старые сохранения (до характеристик предметов): оружию и доспехам подставляем урон и броню каталога.</summary>
    public static void Backfill(RpgState state)
    {
        foreach (var item in state.Grid.Where(i => i.Slot is not null && i.Damage.Length == 0 && i.Armor == 0))
        {
            ApplyBase(item);
        }

        foreach (var (slot, worn) in state.Character.Equipment)
        {
            if (worn.Damage.Length > 0 || worn.Armor > 0)
            {
                continue;
            }

            var (dmg, armor) = Base(worn.Icon);
            if (slot is nameof(EquipSlot.Hand1) or nameof(EquipSlot.Hand2))
            {
                worn.Damage = DamageWords.Localize(dmg);
            }

            worn.Armor = armor;
        }
    }

    // ===== Разбор параметров инструментов =====

    /// <summary>Разбирает «str+1, attack+2, hp+5» (также «СИЛ +1», «ac:1», «мана=10»). Нераспознанное — в Bad.</summary>
    public static (Dictionary<string, int> Bonuses, List<string> Bad) ParseBonuses(string? raw)
    {
        var result = new Dictionary<string, int>();
        var bad = new List<string>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (result, bad);
        }

        foreach (var part in raw.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var i = part.IndexOfAny(new[] { '+', '-', '−', ':', '=', ' ' });
            if (i <= 0)
            {
                bad.Add(part);
                continue;
            }

            var name = part[..i].Trim();
            var num = part[i..].Trim().TrimStart(':', '=').Trim().Replace('−', '-').Replace(" ", "");
            if (!Aliases.TryGetValue(name, out var key) || !int.TryParse(num, out var v))
            {
                bad.Add(part);
                continue;
            }

            result[key] = result.GetValueOrDefault(key) + v;
        }

        return (result, bad);
    }

    /// <summary>Эффекты разделяются «;» или переводом строки (запятые остаются внутри описания).</summary>
    public static List<string> ParseEffects(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? new()
            : raw.Split(new[] { ';', '\n', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    // ===== Описание =====

    public static string BonusText(Dictionary<string, int> bonuses) =>
        string.Join(", ", Keys.Where(bonuses.ContainsKey).Where(k => bonuses[k] != 0).Select(k => $"{KeyTitle(k)} {bonuses[k]:+#;-#}"));

    /// <summary>«урон 1d8 рубящий · КБ +2 · СИЛ +1, атака +1 · ✦ невидимость ночью».</summary>
    public static string Describe(string damage, int armor, Dictionary<string, int> bonuses, List<string> effects)
    {
        var parts = new List<string>();
        if (damage.Length > 0)
        {
            parts.Add($"{Lang.T("урон", "damage")} {damage}");
        }

        if (armor > 0)
        {
            parts.Add($"{Lang.T("КБ", "AC")} +{armor}");
        }

        var b = BonusText(bonuses);
        if (b.Length > 0)
        {
            parts.Add(b);
        }

        parts.AddRange(effects.Select(e => $"✦ {e}"));
        return string.Join(" · ", parts);
    }

    public static string Describe(GridItem i) => Describe(i.Damage, i.Armor, i.Bonuses, i.Effects);

    public static string Describe(EquippedItem i) => Describe(i.Damage, i.Armor, i.Bonuses, i.Effects);

    public static bool HasStats(GridItem i) => i.Damage.Length > 0 || i.Armor > 0 || i.Bonuses.Count > 0 || i.Effects.Count > 0;

    public static bool HasStats(EquippedItem i) => i.Damage.Length > 0 || i.Armor > 0 || i.Bonuses.Count > 0 || i.Effects.Count > 0;

    // ===== Итог по герою =====

    /// <summary>Надетые предметы без повторов (двуручное лежит в обеих руках).</summary>
    public static IEnumerable<(string Slot, EquippedItem Item)> Worn(ICombatant c)
    {
        foreach (var slot in Enum.GetValues<EquipSlot>())
        {
            if (!c.Equipment.TryGetValue(slot.ToString(), out var item))
            {
                continue;
            }

            if (slot == EquipSlot.Hand2 && item.TwoHanded && c.Equipment.ContainsKey(nameof(EquipSlot.Hand1)))
            {
                continue;
            }

            yield return (slot.ToString(), item);
        }
    }

    public static Dictionary<string, int> Totals(ICombatant c)
    {
        var sum = new Dictionary<string, int>();
        foreach (var (_, item) in Worn(c))
        {
            foreach (var (k, v) in item.Bonuses)
            {
                sum[k] = sum.GetValueOrDefault(k) + v;
            }
        }

        return sum;
    }

    private static readonly string[] StatKeys = { "str", "dex", "con", "int", "wis", "cha" };

    public static int Bonus(ICombatant c, DndStat stat) => Totals(c).GetValueOrDefault(StatKeys[(int)stat]);

    /// <summary>Характеристика с учётом предметов (потолок 30).</summary>
    public static int Effective(ICombatant c, DndStat stat) => Math.Clamp(c.Stat(stat) + Bonus(c, stat), 1, 30);

    /// <summary>
    /// Класс брони: 10 + ЛОВ (для средних доспехов не больше +2, для тяжёлых 0) + броня предметов + бонусы ac
    /// + боевой опыт уровня (+1 на 6, 12, 18…).
    /// </summary>
    public static int ArmorClass(ICombatant c)
    {
        var dex = DndStatNames.Modifier(Effective(c, DndStat.Dex));
        if (c.Equipment.TryGetValue(nameof(EquipSlot.Body), out var body))
        {
            if (Heavy.Contains(body.Icon))
            {
                dex = 0;
            }
            else if (Medium.Contains(body.Icon))
            {
                dex = Math.Min(dex, 2);
            }
        }

        return 10 + dex + Worn(c).Sum(w => w.Item.Armor) + Totals(c).GetValueOrDefault("ac") +
               Progression.DefenseBonus(Progression.ParseLevel(c.Level));
    }

    /// <summary>Бонус мастерства по уровню: +2 на 1–4, +3 на 5–8 и т.д. (без потолка — эндгейм растёт дальше).</summary>
    public static int Proficiency(ICombatant c) => Progression.Proficiency(Progression.ParseLevel(c.Level));

    /// <summary>Разобранная атака бойца: бонус, кости (уже умноженные на кости мощи уровня), бонус урона, тип.</summary>
    public sealed record AttackInfo(string Title, int Attack, int Dice, int Die, int Bonus, string Type, bool Spell)
    {
        public string DamageText => $"{Dice}d{Die}{(Bonus == 0 ? "" : Bonus.ToString("+#;-#"))}{(Type.Length > 0 ? " " + Type : "")}";

        public double Average => Dice * (Die + 1) / 2.0 + Bonus;
    }

    private static readonly System.Text.RegularExpressions.Regex DamageRx =
        new(@"(\d*)\s*[dдк]\s*(\d+)\s*(?:[+\-−]\s*\d+)?\s*(.*)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>«2d8+3 рубящий» → (2, 8, «рубящий»); непонятное — 1d4.</summary>
    public static (int Count, int Sides, string Type) ParseDamage(string? damage)
    {
        var m = DamageRx.Match(damage ?? "");
        if (!m.Success)
        {
            return (1, 4, (damage ?? "").Trim());
        }

        var count = m.Groups[1].Value.Length == 0 ? 1 : int.Parse(m.Groups[1].Value);
        return (Math.Clamp(count, 1, 20), Math.Clamp(int.Parse(m.Groups[2].Value), 2, 20), m.Groups[3].Value.Trim());
    }

    /// <summary>Атака оружием в основной руке. Кости оружия умножаются на кости мощи уровня (1d8 → 2d8 с 5-го).</summary>
    public static AttackInfo WeaponAttack(ICombatant c)
    {
        var power = Progression.PowerDice(Progression.ParseLevel(c.Level));
        var totals = Totals(c);
        c.Equipment.TryGetValue(nameof(EquipSlot.Hand1), out var weapon);
        var str = DndStatNames.Modifier(Effective(c, DndStat.Str));
        if (weapon is null || weapon.Damage.Length == 0)
        {
            return new AttackInfo(Lang.T("без оружия", "unarmed"), str + Proficiency(c) + totals.GetValueOrDefault("attack"), power, 4,
                str + totals.GetValueOrDefault("damage"), Lang.T("дробящий", "bludgeoning"), false);
        }

        var dexM = DndStatNames.Modifier(Effective(c, DndStat.Dex));
        var mod = Ranged.Contains(weapon.Icon) ? dexM : Finesse.Contains(weapon.Icon) ? Math.Max(str, dexM) : str;
        var (count, sides, type) = ParseDamage(weapon.Damage);
        return new AttackInfo(weapon.Name, mod + Proficiency(c) + totals.GetValueOrDefault("attack"), count * power, sides,
            mod + totals.GetValueOrDefault("damage"), type, false);
    }

    /// <summary>Боевое заклинание (для магических архетипов): d8 × кости мощи + мод ключевой характеристики.</summary>
    public static AttackInfo? SpellAttack(ICombatant c)
    {
        if (!Progression.ArchetypeOf(c).Spellcaster)
        {
            return null;
        }

        var key = new[] { DndStat.Int, DndStat.Wis, DndStat.Cha }.OrderByDescending(s => Effective(c, s)).First();
        var mod = DndStatNames.Modifier(Effective(c, key));
        var totals = Totals(c);
        var power = Progression.PowerDice(Progression.ParseLevel(c.Level));
        return new AttackInfo(Genre.PickT("заклинание", "программа", "спецприём", "spell", "program", "special move") + $" ({DndStatNames.Short[(int)key]})", mod + Proficiency(c) + totals.GetValueOrDefault("attack"),
            power, 8, mod + totals.GetValueOrDefault("damage"), "", true);
    }

    /// <summary>Лучшая атака бойца — по ожидаемому урону с учётом точности.</summary>
    public static AttackInfo BestAttack(ICombatant c)
    {
        var weapon = WeaponAttack(c);
        return SpellAttack(c) is { } spell && spell.Average + spell.Attack > weapon.Average + weapon.Attack ? spell : weapon;
    }

    /// <summary>DC спасброска против навыков бойца: 8 + мастерство + лучшая из двух ключевых характеристик архетипа.</summary>
    public static int SaveDc(ICombatant c)
    {
        var best = Progression.ArchetypeOf(c).Priority.Take(2).Max(s => DndStatNames.Modifier(Effective(c, s)));
        return 8 + Proficiency(c) + best;
    }

    /// <summary>«Длинный меч: атака +5, урон 1d8+3 рубящий» (+ заклинание у магов, кость мощи и DC навыков).</summary>
    public static string AttackLine(ICombatant c)
    {
        var w = WeaponAttack(c);
        var line = $"{w.Title}: {Lang.T("атака", "attack")} {w.Attack:+#;-#;+0}, {Lang.T("урон", "damage")} {w.DamageText}";
        if (SpellAttack(c) is { } s)
        {
            line += $"; {s.Title}: {Lang.T("атака", "attack")} {s.Attack:+#;-#;+0}, {Lang.T("урон", "damage")} {s.DamageText}";
        }

        return line + Lang.T($"; кость мощи К = {Progression.PowerDice(Progression.ParseLevel(c.Level))}d8, DC навыков {SaveDc(c)}",
                             $"; power die K = {Progression.PowerDice(Progression.ParseLevel(c.Level))}d8, skill DC {SaveDc(c)}");
    }

    /// <summary>Особые свойства всех надетых вещей.</summary>
    public static List<(string Item, string Effect)> ActiveEffects(ICombatant c) =>
        Worn(c).SelectMany(w => w.Item.Effects.Select(e => (w.Item.Name, e))).ToList();

    /// <summary>Надевание/снятие вещи с бонусом HP или маны меняет максимум (и текущее значение на ту же величину).</summary>
    public static void ApplyVitals(CharacterSheet c, EquippedItem item, int sign)
    {
        var hp = item.Bonuses.GetValueOrDefault("hp") * sign;
        var mana = item.Bonuses.GetValueOrDefault("mana") * sign;
        if (hp != 0)
        {
            c.HpMax = Math.Max(1, c.HpMax + hp);
            c.HpCurrent = Math.Clamp(c.HpCurrent + hp, sign < 0 ? 1 : 0, c.HpMax);
        }

        if (mana != 0)
        {
            c.ManaMax = Math.Max(0, c.ManaMax + mana);
            c.ManaCurrent = Math.Clamp(c.ManaCurrent + mana, 0, c.ManaMax);
        }
    }
}
