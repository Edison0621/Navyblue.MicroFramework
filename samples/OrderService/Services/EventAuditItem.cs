namespace OrderService.Services;

public sealed record EventAuditItem(
    string Topic,
    string EventId,
    string OrderId,
    string ProductId,
    int Quantity,
    DateTimeOffset ReceivedAt);
