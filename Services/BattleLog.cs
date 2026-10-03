namespace RPG_Harness.Services;

/// <summary>Одна запись боевого журнала (лог боя).</summary>
public sealed class BattleLogEntry
{
    /// <summary>Сквозной номер: по нему окно боя понимает, какие записи новые (для анимаций).</summary>
    public long Id { get; set; }

    public string ChatId { get; set; } = "";

    /// <summary>roll | initiative | damage | heal | shield | buff | debuff | cleanse | revive | defeat | turn | loot | info</summary>
    public string Kind { get; set; } = "info";

    public string Icon { get; set; } = "•";

    /// <summary>Готовая строка для записей без структуры (ход игрока, добыча, выбытие).</summary>
    public string Text { get; set; } = "";

    /// <summary>Кто действует: «Гоблин 1», «Лира». Пусто — действие без явного источника.</summary>
    public string? Actor { get; set; }

    /// <summary>Кого касается действие: «Герой», «Гоблин 1». Пусто — строка строится из Text.</summary>
    public string? Subject { get; set; }

    /// <summary>Крупная величина справа: «7 урона», «+12 жизни», «щит 8», «18».</summary>
    public string? Value { get; set; }

    /// <summary>Итог броска словами: «попадание», «промах», «крит!», «успех», «провал». Пусто — итог неочевиден.</summary>
    public string? Verdict { get; set; }

    /// <summary>Плохой ли итог для действующего (промах/провал) — для цвета метки.</summary>
    public bool VerdictBad { get; set; }

    /// <summary>Пояснение мелким шрифтом: математика броска («15 + 3 = 18»), остаток HP.</summary>
    public string? Detail { get; set; }

    /// <summary>
    /// Урон, нанесённый этой атакой. Стоит прямо в строке проверки класса брони, чтобы было видно,
    /// сколько именно сняли, — а не отдельной строкой ниже.
    /// </summary>
    public int? Damage { get; set; }

    /// <summary>«@hero», имя спутника или имя противника — чтобы анимировать нужную карточку в окне боя.</summary>
    public string? Target { get; set; }

    /// <summary>Число события: результат броска, урон, лечение, щит.</summary>
    public int? Amount { get; set; }

    /// <summary>Короткая подпись для оверлея кубика (что именно бросали).</summary>
    public string? Label { get; set; }

    /// <summary>Натуральная 20 на броске d20.</summary>
    public bool Crit { get; set; }

    /// <summary>Натуральная 1 на броске d20.</summary>
    public bool Fail { get; set; }

    public DateTime At { get; set; } = DateTime.Now;
}

/// <summary>
/// Боевой журнал: кто кого ударил, вылечил, наложил или снял эффект. Живёт отдельно от истории чата
/// и не пишется в файл — это живая лента текущего боя (переживает переходы внутри приложения).
/// </summary>
public sealed class BattleLog
{
    private const int MaxEntries = 300;
    private readonly object _gate = new();
    private readonly Dictionary<string, List<BattleLogEntry>> _byChat = new();
    private long _seq;

    /// <summary>Журнал изменился. Аргумент — id чата.</summary>
    public event Action<string>? Changed;

    /// <summary>Записи чата в хронологическом порядке (старые → новые).</summary>
    public IReadOnlyList<BattleLogEntry> Get(string chatId)
    {
        lock (_gate)
        {
            return _byChat.TryGetValue(chatId, out var list) && list.Count > 0
                ? list.ToArray()
                : Array.Empty<BattleLogEntry>();
        }
    }

    /// <summary>Номер последней записи в чате (0 — журнал пуст).</summary>
    public long LastId(string chatId)
    {
        lock (_gate)
        {
            return _byChat.TryGetValue(chatId, out var list) && list.Count > 0 ? list[^1].Id : 0;
        }
    }

    public BattleLogEntry Add(
        string chatId,
        string kind,
        string icon,
        string text,
        string? target = null,
        int? amount = null,
        string? label = null,
        bool crit = false,
        bool fail = false,
        string? actor = null,
        string? subject = null,
        string? value = null,
        string? detail = null,
        string? verdict = null,
        bool verdictBad = false)
    {
        BattleLogEntry entry;
        lock (_gate)
        {
            if (!_byChat.TryGetValue(chatId, out var list))
            {
                list = new List<BattleLogEntry>();
                _byChat[chatId] = list;
            }

            entry = new BattleLogEntry
            {
                Id = ++_seq,
                ChatId = chatId,
                Kind = kind,
                Icon = icon,
                Text = text,
                Target = target,
                Amount = amount,
                Label = label,
                Crit = crit,
                Fail = fail,
                Actor = actor,
                Subject = subject,
                Value = value,
                Detail = detail,
                Verdict = verdict,
                VerdictBad = verdictBad,
            };

            list.Add(entry);
            if (list.Count > MaxEntries)
            {
                list.RemoveRange(0, list.Count - MaxEntries);
            }
        }

        Changed?.Invoke(chatId);
        return entry;
    }

    /// <summary>
    /// Дописать урон к конкретной записи (броску атаки). Нужно, чтобы число урона стояло рядом
    /// с проверкой класса брони, а не отдельной строкой ниже.
    /// </summary>
    public bool AppendDamage(string chatId, long entryId, int damage)
    {
        if (damage <= 0)
        {
            return false;
        }

        lock (_gate)
        {
            if (!_byChat.TryGetValue(chatId, out var list))
            {
                return false;
            }

            var entry = list.FirstOrDefault(e => e.Id == entryId);
            if (entry is null)
            {
                return false;
            }

            entry.Damage = (entry.Damage ?? 0) + damage;
        }

        Changed?.Invoke(chatId);
        return true;
    }

    /// <summary>Очистить журнал боя (кнопка в окне боя).</summary>
    public void Clear(string chatId)
    {
        lock (_gate)
        {
            if (!_byChat.TryGetValue(chatId, out var list) || list.Count == 0)
            {
                return;
            }

            list.Clear();
        }

        Changed?.Invoke(chatId);
    }
}
