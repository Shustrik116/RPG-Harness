namespace RPG_Harness.Services;

/// <summary>
/// Передаёт нажатие кнопки «Начать разговор» из панели в открытый чат:
/// там игрокское действие превращается в ход, а ГМ сам приходит, чем эта встреча будет.
/// </summary>
public sealed class TalkFlow
{
    /// <summary>chatId, имя NPC рядом.</summary>
    public event Action<string, string>? TalkRequested;

    public void Request(string chatId, string npc) => TalkRequested?.Invoke(chatId, npc);
}