namespace RPG_Harness.Services;

/// <summary>Передаёт просьбу игрока расстаться со спутником в основной чат, где решение отыгрывает ГМ.</summary>
public sealed class PartyFlow
{
    public event Action<string, string, string>? LeaveRequested;
    public event Action<PartyItemRequest>? ItemActionRequested;
    public void RequestLeave(string chatId, string member, string reason) => LeaveRequested?.Invoke(chatId, member, reason);
    public void RequestItemAction(PartyItemRequest request) => ItemActionRequested?.Invoke(request);
}

/// <summary>Запрос игрока, который должен быть отыгран ГМ до изменения самостоятельного спутника.</summary>
public sealed record PartyItemRequest(
    string ChatId,
    string Action,
    string Member,
    string ItemId,
    string ItemName,
    int Quantity,
    string Target = "");
