namespace OrderService.Models;

public sealed record OrderDeliveredEvent(
    string OrderId,
    string SubOrderId,
    string ShopId,
    string? UserId,
    DateTimeOffset OccurredAt);
