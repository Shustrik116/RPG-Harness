namespace RPG_Harness.Services;

/// <summary>
/// Шина окна торговли (на одну вкладку браузера): открыть лавку NPC — из чата (мастер вызвал open_trade)
/// или из книги героя; по закрытию окна итог сделки уходит в чат, чтобы мастер отыграл реакцию торговца.
/// </summary>
public sealed class TradeFlow
{
    /// <summary>Запрошено окно торговли: (id чата, имя торговца).</summary>
    public event Action<string, string>? OpenRequested;

    /// <summary>Сделка завершена: (id чата, текст итога для мастера).</summary>
    public event Action<string, string>? Completed;

    public void Open(string chatId, string npcName) => OpenRequested?.Invoke(chatId, npcName);

    public void Complete(string chatId, string summary) => Completed?.Invoke(chatId, summary);
}
