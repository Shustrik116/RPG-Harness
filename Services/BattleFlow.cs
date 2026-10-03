namespace RPG_Harness.Services;
public sealed class BattleFlow
{
    // Последнее известное состояние по чату: окно боя открывается в любой момент и должно сразу показать его.
    private readonly Dictionary<string,string> _status=new(StringComparer.Ordinal);
    private readonly Dictionary<string,string> _stream=new(StringComparer.Ordinal);

    public event Action<string>? OpenRequested;
    public event Action<string,string>? ActionRequested;

    /// <summary>Статус чата («Рассказчик думает…», пусто — готов). Окно боя показывает его в своей шапке.</summary>
    public event Action<string,string>? StatusChanged;

    /// <summary>
    /// Поток текста мастера в окно боя: (chatId, накопленный текст текущей реплики).
    /// Пустая строка — поток завершён или начинается новая реплика. Окно боя печатает его с той же анимацией, что и основной чат.
    /// </summary>
    public event Action<string,string>? StreamChanged;

    public void Open(string chatId)=>OpenRequested?.Invoke(chatId);
    public void Act(string chatId,string action)=>ActionRequested?.Invoke(chatId,action);

    public void SetStatus(string chatId,string status)
    {
        _status[chatId]=status;
        StatusChanged?.Invoke(chatId,status);
    }

    public void SetStream(string chatId,string text)
    {
        _stream[chatId]=text;
        StreamChanged?.Invoke(chatId,text);
    }

    /// <summary>Текущий статус чата (пусто — мастер готов) — чтобы окно боя не теряло его при открытии.</summary>
    public string StatusOf(string chatId)=>_status.TryGetValue(chatId,out var s)?s:"";

    /// <summary>Текущий поток текста мастера (пусто — поток не идёт).</summary>
    public string StreamOf(string chatId)=>_stream.TryGetValue(chatId,out var s)?s:"";
}
