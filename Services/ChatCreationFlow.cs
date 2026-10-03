namespace RPG_Harness.Services;

/// <summary>
/// Шина «создать новый чат»: любой UI (сайдбар, экран приветствия) вызывает Request(),
/// а модальный редактор персонажа подписан на Requested. Персонаж задаётся только при создании чата.
/// После создания героя чат помечается для автостарта: мастер сам открывает сессию ноль.
/// </summary>
public sealed class ChatCreationFlow
{
    private readonly HashSet<string> _pendingKickoff = new();
    private readonly object _lock = new();

    /// <summary>Вызывается, когда пользователь хочет создать новый чат.</summary>
    public event Action? Requested;

    public void Request() => Requested?.Invoke();

    /// <summary>Отметить новый чат: при первом открытии мастер начнёт разговор сам.</summary>
    public void MarkKickoff(string chatId)
    {
        lock (_lock)
        {
            _pendingKickoff.Add(chatId);
        }
    }

    /// <summary>Забрать отметку автостарта (true — один раз на чат).</summary>
    public bool TakeKickoff(string chatId)
    {
        lock (_lock)
        {
            return _pendingKickoff.Remove(chatId);
        }
    }
}
