namespace RPG_Harness.Services;

/// <summary>
/// Подсказка к найденной вещи: кому из группы она станет улучшением, а если никому — кому её продать.
/// Так добыча сама делится на «надеть» и «продать», и игроку не нужно сверять цифры вручную.
/// Сравнение честное и грубое: очки бонусов + броня + средний урон, только внутри одного «семейства»
/// (меч сравнивается с мечом, а не с посохом; мантия — с мантией, а не с латами).
/// </summary>
public static class LootAdvisor
{
    public enum AdviceKind { None, Upgrade, Sell }

    public sealed record Advice(AdviceKind Kind, string Text, string Short);

    /// <summary>Сила вещи для сравнения: очки бюджета бонусов, броня ×2, половина среднего урона, свойства.</summary>
    public static double Score(string damage, int armor, Dictionary<string, int> bonuses, List<string> effects)
    {
        var (count, sides, _) = ItemStats.ParseDamage(damage);
        var avg = count > 0 && sides > 0 ? count * (sides + 1) / 2.0 : 0;
        return ItemStats.Spent(bonuses) + armor * 2 + avg * 0.5 + effects.Count * 1.5;
    }

    public static double Score(GridItem i) => Score(i.Damage, i.Armor, i.Bonuses, i.Effects);
    public static double Score(EquippedItem i) => Score(i.Damage, i.Armor, i.Bonuses, i.Effects);

    /// <summary>Семейство вещи для сравнения: ближний бой / стрелковое / магия; вес доспеха; щит.</summary>
    public static string Family(ItemEconomy economy, string? icon, EquipSlot? slot)
    {
        var cat = economy.CategoryOf(icon);
        return slot switch
        {
            EquipSlot.Hand1 => ItemStats.IsRanged(icon) && cat != "magic" ? "ranged" : cat switch { "ranged" => "ranged", "magic" => "magic", _ => "melee" },
            EquipSlot.Hand2 => cat == "shields" ? "shield" : "offhand",
            EquipSlot.Body => cat,
            _ => "any",
        };
    }

    /// <summary>Все, кто носит снаряжение: герой и спутники.</summary>
    public static IEnumerable<ICombatant> Wearers(RpgState state)
    {
        yield return state.Character;
        foreach (var m in state.Party) yield return m;
    }

    /// <summary>Во что сейчас одет боец в слоте находки (для колец — более слабое из двух). null — слот пуст.</summary>
    public static EquippedItem? Current(ICombatant c, EquipSlot slot)
    {
        if (slot is EquipSlot.Ring1 or EquipSlot.Ring2)
        {
            c.Equipment.TryGetValue(nameof(EquipSlot.Ring1), out var r1);
            c.Equipment.TryGetValue(nameof(EquipSlot.Ring2), out var r2);
            if (r1 is null || r2 is null) return null;
            return Score(r1) <= Score(r2) ? r1 : r2;
        }

        return c.Equipment.TryGetValue(slot.ToString(), out var item) ? item : null;
    }

    /// <summary>
    /// Станет ли вещь улучшением для бойца: тот же слот и семейство, сила выше надетого.
    /// replaced — что придётся снять (null — слот пуст).
    /// </summary>
    public static bool IsUpgrade(ItemEconomy economy, ICombatant c, GridItem item, out EquippedItem? replaced)
    {
        replaced = null;
        if (item.Slot is not { } slot) return false;

        // Щит не наденешь при двуручном оружии; двуручное не берём тому, кто держит щит.
        c.Equipment.TryGetValue(nameof(EquipSlot.Hand1), out var hand1);
        c.Equipment.TryGetValue(nameof(EquipSlot.Hand2), out var hand2);
        if (slot == EquipSlot.Hand2 && hand1 is { TwoHanded: true }) return false;
        if (slot == EquipSlot.Hand1 && item.TwoHanded && hand2 is { TwoHanded: false } && economy.CategoryOf(hand2.Icon) == "shields") return false;

        replaced = Current(c, slot);
        if (replaced is null)
        {
            // Пустые руки или торс — сверяемся с архетипом: лучнику не советуем кистень, магу — латы.
            var arch = Progression.ArchetypeOf(c);
            var typical = slot == EquipSlot.Hand1 ? arch.Weapon : slot == EquipSlot.Body ? arch.Armor : null;
            if (typical is not null && Family(economy, typical, slot) != Family(economy, item.Icon, slot)) return false;
            return ItemStats.HasStats(item);
        }

        if (Family(economy, replaced.Icon, slot) != Family(economy, item.Icon, slot)) return false;
        return Score(item) > Score(replaced) + 0.5;
    }

    /// <summary>Кто из торговцев лучше всех купит вещь и сколько даст (без поправки на Харизму).</summary>
    public static (string Who, int Price) BestBuyer(ItemEconomy economy, GridItem item)
    {
        var cat = economy.CategoryOf(item.Icon);
        var best = (Preferred.TryGetValue(cat, out var key) ? ItemEconomy.Merchant(key) : null)
                   ?? ItemEconomy.Merchants.Where(m => m.Key != "fence" && m.Main.Contains(cat)).MaxBy(m => m.BuyRate)
                   ?? ItemEconomy.Merchant("fence");
        var price = Math.Max(1, (int)Math.Floor(economy.UnitValue(item) * best.BuyRate));
        return (Dative(best.Key), price);
    }

    /// <summary>Когда несколько торговцев платят одинаково — к кому идти естественнее.</summary>
    private static Dictionary<string, string> Preferred => Genre.Pick(FantasyPreferred, CyberPreferred, ModernPreferred);

    private static readonly Dictionary<string, string> ModernPreferred = new()
        { ["ranged"] = "gunshop", ["light_armor"] = "outfitter", ["materials"] = "autoparts", ["potions"] = "pharmacy", ["food"] = "general", ["gems"] = "pawnshop", ["jewelry"] = "pawnshop", ["curios"] = "pawnshop" };

    private static readonly Dictionary<string, string> FantasyPreferred = new() { ["light_armor"] = "tailor", ["curios"] = "alchemist" };

    private static readonly Dictionary<string, string> CyberPreferred = new()
        { ["light_armor"] = "outfitter", ["materials"] = "techshop", ["potions"] = "ripperdoc", ["food"] = "bar", ["curios"] = "boutique" };

    private static string Dative(string merchant) => Lang.IsEn ? DativeEn(merchant) : merchant switch
    {
        "blacksmith" => "кузнецу",
        "bowyer" => "оружейнику-лучнику",
        "tailor" => "портному",
        "alchemist" => "алхимику",
        "mage" => "чародею",
        "jeweler" => "ювелиру",
        "general" => Genre.Pick("лавочнику", "в уличный ларёк", "в магазин"),
        "innkeeper" => "трактирщику",
        "scribe" => "книжнику",
        "temple" => "в храм",
        "gunsmith" => "оружейнику",
        "outfitter" => "в магазин экипировки",
        "ripperdoc" => "риппердоку",
        "netdealer" => "нетраннеру-барыге",
        "pharmacy" => "в аптеку",
        "techshop" => "в магазин электроники",
        "boutique" => "в скупку ценностей",
        "bar" => "в бар",
        "gunshop" => "в оружейный магазин",
        "blackmarket" => "подпольному торговцу оружием",
        "hardware" => "в хозяйственный",
        "electronics" => "в магазин электроники",
        "pawnshop" => "в ломбард",
        "autoparts" => "в автозапчасти",
        _ => "скупщику",
    };

    private static string DativeEn(string merchant) => merchant switch
    {
        "blacksmith" => "to a blacksmith",
        "bowyer" => "to a bowyer",
        "tailor" => "to a tailor",
        "alchemist" => "to an alchemist",
        "mage" => "to an enchanter",
        "jeweler" => "to a jeweler",
        "general" => Genre.Pick("to a shopkeeper", "to a street stall", "to a store"),
        "innkeeper" => "to an innkeeper",
        "scribe" => "to a bookseller",
        "temple" => "to a temple",
        "gunsmith" => "to a gunsmith",
        "outfitter" => "to a gear shop",
        "ripperdoc" => "to a ripperdoc",
        "netdealer" => "to a netrunner dealer",
        "pharmacy" => "to a pharmacy",
        "techshop" => "to an electronics shop",
        "boutique" => "to a valuables buyer",
        "bar" => "to a bar",
        "gunshop" => "to a gun store",
        "blackmarket" => "to an underground arms dealer",
        "hardware" => "to a hardware store",
        "electronics" => "to an electronics store",
        "pawnshop" => "to a pawnshop",
        "autoparts" => "to an auto parts store",
        _ => "to a fence",
    };

    /// <summary>Подсказка к вещи. Расходники и сюжетные мелочи без цены остаются без подсказки.</summary>
    public static Advice For(ItemEconomy economy, RpgState state, GridItem item)
    {
        if (item.Slot is not null)
        {
            var empty = new List<string>();
            var better = new List<string>();
            foreach (var c in Wearers(state))
            {
                if (!IsUpgrade(economy, c, item, out var old)) continue;
                var who = ReferenceEquals(c, state.Character) ? Lang.T("герой", "hero") : c.Name;
                if (old is null) empty.Add(who); else better.Add(Lang.T($"{who} — лучше «{old.Name}»", $"{who} — better than \"{old.Name}\""));
            }

            if (empty.Count + better.Count > 0)
            {
                var parts = better.ToList();
                if (empty.Count > 0) parts.Add(string.Join(", ", empty) + Lang.T(" — слот пуст", " — the slot is empty"));
                var shorts = better.Select(b => b[..b.IndexOf(" — ")]).Concat(empty);
                return new Advice(AdviceKind.Upgrade, Lang.T("↑ подойдёт: ", "↑ suits: ") + string.Join("; ", parts), "↑ " + string.Join(", ", shorts));
            }
        }

        if (item.Consumable || ItemEconomy.Category(economy.CategoryOf(item.Icon))?.Key is "potions" or "scrolls")
        {
            return new Advice(AdviceKind.None, "", "");
        }

        var (buyer, price) = BestBuyer(economy, item);
        var qty = Math.Max(1, item.Quantity);
        var text = Lang.T($"на продажу: {buyer} ~{price * qty} з", $"for sale: {buyer} ~{price * qty} {Genre.Coin}");
        return new Advice(AdviceKind.Sell, text, text);
    }
}
