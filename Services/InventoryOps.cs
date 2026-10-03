namespace RPG_Harness.Services;

/// <summary>
/// Операции инвентаря-тетриса и экипировки, общие для панели «Книга героя» и инструментов модели:
/// поиск места на сетке, надеть предмет из сумки (надетое ранее возвращается в сумку), снять в сумку.
/// </summary>
public static class InventoryOps
{
    public const int Cols = RpgState.GridCols;
    public const int Rows = RpgState.GridRows;

    /// <summary>Помещается ли прямоугольник w x h в клетку (col,row), не задевая другие предметы.</summary>
    public static bool Fits(IEnumerable<GridItem> items, int col, int row, int w, int h, GridItem? ignore = null,
                            int cols = Cols, int rows = Rows)
    {
        if (col < 0 || row < 0 || col + w > cols || row + h > rows)
        {
            return false;
        }

        return !items.Any(i => !ReferenceEquals(i, ignore) &&
                               col < i.Col + i.W && i.Col < col + w &&
                               row < i.Row + i.H && i.Row < row + h);
    }

    /// <summary>Первое свободное место (сначала — предпочтительная клетка, если задана).</summary>
    public static (int Col, int Row)? FindFree(
        IEnumerable<GridItem> items, int w, int h, GridItem? ignore = null, int? preferCol = null, int? preferRow = null,
        int cols = Cols, int rows = Rows)
    {
        var list = items as IList<GridItem> ?? items.ToList();
        w = Math.Clamp(w, 1, cols);
        h = Math.Clamp(h, 1, rows);

        if (preferCol is { } pc && preferRow is { } pr && Fits(list, pc, pr, w, h, ignore, cols, rows))
        {
            return (pc, pr);
        }

        // По колонкам сверху вниз, как раскладывают сумку в Diablo: длинные вещи встают вертикально рядом.
        for (var col = 0; col <= cols - w; col++)
        {
            for (var row = 0; row <= rows - h; row++)
            {
                if (Fits(list, col, row, w, h, ignore, cols, rows))
                {
                    return (col, row);
                }
            }
        }

        return null;
    }

    /// <summary>Можно ли положить предмет с «родным» слотом itemSlot в слот target.</summary>
    public static bool CanGoTo(EquipSlot? itemSlot, bool twoHanded, EquipSlot target)
    {
        if (itemSlot is null)
        {
            return false;
        }

        if (twoHanded)
        {
            return target is EquipSlot.Hand1 or EquipSlot.Hand2;
        }

        return (itemSlot.Value, target) switch
        {
            (EquipSlot.Ring1 or EquipSlot.Ring2, EquipSlot.Ring1 or EquipSlot.Ring2) => true,
            (EquipSlot.Hand1, EquipSlot.Hand1 or EquipSlot.Hand2) => true, // вторая рука — парное оружие
            var (a, b) => a == b,
        };
    }

    /// <summary>
    /// Надевает предмет. Если item лежит в сумке — убирает его оттуда; надетое ранее в занимаемых слотах
    /// возвращается в сумку. При нехватке места ничего не меняет и возвращает false.
    /// </summary>
    public static bool Equip(RpgState state, GridItem item, EquipSlot? target, ItemCatalog? catalog, out string message)
    {
        var eq = state.Character.Equipment;
        var home = item.Slot ?? catalog?.Get(item.Icon)?.EquipSlot;
        if (home is null && target is null)
        {
            message = Lang.T($"«{item.Name}» нельзя надеть — у предмета нет слота экипировки.", $"\"{item.Name}\" cannot be worn — the item has no equipment slot.");
            return false;
        }

        home ??= target;

        // Какие слоты займёт предмет.
        EquipSlot[] slots;
        if (item.TwoHanded)
        {
            slots = new[] { EquipSlot.Hand1, EquipSlot.Hand2 };
        }
        else
        {
            var slot = target ?? home!.Value;
            if (!CanGoTo(home, false, slot))
            {
                message = Lang.T($"«{item.Name}» не подходит для слота {SlotTitle(slot)}.", $"\"{item.Name}\" does not fit the {SlotTitle(slot)} slot.");
                return false;
            }

            // Второе кольцо — в свободный палец.
            if (target is null && slot == EquipSlot.Ring1 && eq.ContainsKey(nameof(EquipSlot.Ring1)) && !eq.ContainsKey(nameof(EquipSlot.Ring2)))
            {
                slot = EquipSlot.Ring2;
            }

            slots = new[] { slot };
        }

        // Что будет снято: предметы в этих слотах + двуручное, которое держит вторую руку.
        var displacedKeys = new HashSet<string>(slots.Select(s => s.ToString()));
        foreach (var hand in new[] { EquipSlot.Hand1, EquipSlot.Hand2 })
        {
            if (slots.Contains(hand) && eq.TryGetValue(hand.ToString(), out var held) && held.TwoHanded)
            {
                displacedKeys.Add(nameof(EquipSlot.Hand1));
                displacedKeys.Add(nameof(EquipSlot.Hand2));
            }
        }

        var displaced = displacedKeys
            .Select(k => (Key: k, Item: eq.GetValueOrDefault(k)))
            .Where(x => x.Item is not null)
            // После загрузки сохранения двуручное в Hand1 и Hand2 — разные объекты: возвращаем его один раз.
            .Where(x => !(x.Key == nameof(EquipSlot.Hand2) && x.Item!.TwoHanded && displacedKeys.Contains(nameof(EquipSlot.Hand1))
                          && eq.ContainsKey(nameof(EquipSlot.Hand1))))
            .GroupBy(x => x.Item!)
            .Select(g => (Item: g.Key, Slot: Enum.Parse<EquipSlot>(g.First().Key)))
            .ToList();

        // Пробная раскладка: предмет уходит из сумки, снятое должно в неё поместиться.
        var splitStack = state.Grid.Contains(item) && item.Quantity > 1;
        var bag = state.Grid.Where(i => !ReferenceEquals(i, item) || splitStack).ToList();
        var returned = new List<GridItem>();
        foreach (var (old, oldSlot) in displaced)
        {
            var back = ToGridItem(old, oldSlot, catalog);
            var spot = FindFree(bag, back.W, back.H);
            if (spot is null)
            {
                message = Lang.T($"Нет места в сумке, чтобы снять «{old.Name}» ({back.W}x{back.H}).", $"No room in the bag to take off \"{old.Name}\" ({back.W}x{back.H}).");
                return false;
            }

            (back.Col, back.Row) = spot.Value;
            bag.Add(back);
            returned.Add(back);
        }

        if (splitStack)
        {
            item.Quantity--;
        }
        else
        {
            state.Grid.Remove(item);
        }
        state.Grid.AddRange(returned);
        foreach (var (old, _) in displaced)
        {
            ItemStats.ApplyVitals(state.Character, old, -1);
        }

        foreach (var key in displacedKeys)
        {
            eq.Remove(key);
        }

        var worn = new EquippedItem
        {
            Name = item.Name,
            Icon = item.Icon,
            Note = item.Note,
            TwoHanded = item.TwoHanded,
            W = item.W,
            H = item.H,
            Value = item.Value,
            Rarity = item.Rarity,
            Damage = item.Damage,
            Armor = item.Armor,
            Bonuses = new(item.Bonuses),
            Effects = new(item.Effects),
            Level = item.Level,
        };
        foreach (var s in slots)
        {
            eq[s.ToString()] = worn;
        }

        ItemStats.ApplyVitals(state.Character, worn, +1);

        message = Lang.T($"надето «{item.Name}» ({(item.TwoHanded ? "обе руки" : SlotTitle(slots[0]))})", $"equipped \"{item.Name}\" ({(item.TwoHanded ? "both hands" : SlotTitle(slots[0]))})") +
                  (returned.Count > 0 ? Lang.T("; в сумку: ", "; to the bag: ") + string.Join(", ", returned.Select(r => r.Name)) : "");
        return true;
    }

    /// <summary>Снимает предмет со слота в сумку. Нет места — предмет остаётся надетым (или выбрасывается при drop).</summary>
    public static bool Unequip(RpgState state, EquipSlot slot, ItemCatalog? catalog, bool drop, out string message)
    {
        var eq = state.Character.Equipment;
        if (!eq.TryGetValue(slot.ToString(), out var worn))
        {
            message = Lang.T($"Слот {SlotTitle(slot)} и так пуст.", $"The {SlotTitle(slot)} slot is already empty.");
            return false;
        }

        var keys = worn.TwoHanded
            ? new[] { nameof(EquipSlot.Hand1), nameof(EquipSlot.Hand2) }
            : new[] { slot.ToString() };

        if (!drop)
        {
            var back = ToGridItem(worn, slot, catalog);
            var stack = state.Grid.FirstOrDefault(i => CanStack(i, back));
            if (stack is not null)
            {
                stack.Quantity++;
            }
            else
            {
                var spot = FindFree(state.Grid, back.W, back.H);
                if (spot is null)
                {
                    message = Lang.T($"Нет места в сумке для «{worn.Name}» ({back.W}x{back.H}) — предмет остаётся надетым.", $"No room in the bag for \"{worn.Name}\" ({back.W}x{back.H}) — the item stays equipped.");
                    return false;
                }

                (back.Col, back.Row) = spot.Value;
                state.Grid.Add(back);
            }
        }

        foreach (var k in keys)
        {
            eq.Remove(k);
        }

        ItemStats.ApplyVitals(state.Character, worn, -1);

        message = drop ? Lang.T($"«{worn.Name}» снят и выброшен.", $"\"{worn.Name}\" taken off and discarded.") : Lang.T($"«{worn.Name}» снят в сумку.", $"\"{worn.Name}\" taken off into the bag.");
        return true;
    }

    /// <summary>Надетый предмет → предмет сумки (размер из сохранённого, иначе из каталога).</summary>
    public static GridItem ToGridItem(EquippedItem worn, EquipSlot slot, ItemCatalog? catalog)
    {
        var def = catalog?.Get(worn.Icon);
        var w = worn.W;
        var h = worn.H;
        if (w <= 1 && h <= 1 && def is not null)
        {
            // Сохранения до появления размеров: берём размер из каталога.
            w = def.W;
            h = def.H;
        }

        return new GridItem
        {
            Name = worn.Name,
            Icon = worn.Icon,
            Note = worn.Note,
            W = w,
            H = h,
            TwoHanded = worn.TwoHanded,
            Value = worn.Value,
            Rarity = worn.Rarity,
            Damage = worn.Damage,
            Armor = worn.Armor,
            Bonuses = new(worn.Bonuses),
            Effects = new(worn.Effects),
            Level = worn.Level,
            Slot = worn.TwoHanded ? EquipSlot.Hand1 : def?.EquipSlot ?? (slot == EquipSlot.Ring2 ? EquipSlot.Ring1 : slot),
        };
    }

    public static bool CanStack(GridItem a, GridItem b) =>
        a.Name.Equals(b.Name, StringComparison.OrdinalIgnoreCase) &&
        a.Icon == b.Icon && a.W == b.W && a.H == b.H && a.Slot == b.Slot && a.TwoHanded == b.TwoHanded &&
        a.Value == b.Value && a.Rarity == b.Rarity && a.Damage == b.Damage && a.Armor == b.Armor && a.Note == b.Note && a.Consumable == b.Consumable &&
        a.Bonuses.OrderBy(x => x.Key).SequenceEqual(b.Bonuses.OrderBy(x => x.Key)) &&
        a.Effects.SequenceEqual(b.Effects);

    /// <summary>Копия предмета для атомарной передачи между сумками.</summary>
    public static GridItem CloneForTransfer(GridItem item, int quantity = 1) => new()
    {
        Name = item.Name, Icon = item.Icon, W = item.W, H = item.H, Quantity = Math.Max(1, quantity),
        Note = item.Note, Slot = item.Slot, TwoHanded = item.TwoHanded, Consumable = item.Consumable,
        Value = item.Value, Rarity = item.Rarity, Damage = item.Damage, Armor = item.Armor,
        Bonuses = new(item.Bonuses), Effects = new(item.Effects), Level = item.Level,
    };

    public static string SlotTitle(EquipSlot slot) => slot switch
    {
        EquipSlot.Helmet => Lang.T("голова", "head"),
        EquipSlot.Body => Lang.T("торс", "torso"),
        EquipSlot.Gloves => Lang.T("руки", "hands"),
        EquipSlot.Boots => Lang.T("ноги", "feet"),
        EquipSlot.Amulet => Lang.T("шея", "neck"),
        EquipSlot.Ring1 => Lang.T("кольцо I", "ring I"),
        EquipSlot.Ring2 => Lang.T("кольцо II", "ring II"),
        EquipSlot.Belt => Lang.T("пояс", "belt"),
        EquipSlot.Hand1 => Lang.T("основная рука", "main hand"),
        EquipSlot.Hand2 => Lang.T("вторая рука", "off hand"),
        EquipSlot.Cloak => Lang.T("плащ", "cloak"),
        _ => slot.ToString(),
    };
}
