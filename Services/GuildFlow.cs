namespace RPG_Harness.Services;

public sealed class GuildFlow
{
    public event Action<string, string>? OpenRequested;
    public event Action<string, string>? Completed;
    public void Open(string chatId, string city) => OpenRequested?.Invoke(chatId, city);
    public void Complete(string chatId, string summary) => Completed?.Invoke(chatId, summary);
}
