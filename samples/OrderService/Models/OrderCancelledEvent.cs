namespace OrderService.Models;

public sealed record OrderCancelledEvent(
    string OrderId,
    string? UserId,
    bool FullOrder,
    string? SubOrderId,
    DateTimeOffset OccurredAt);
