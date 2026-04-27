namespace OrderService.Models;

public sealed record OrderShippedEvent(
    string OrderId,
    string SubOrderId,
    string ShopId,
    string? UserId,
    string? TrackingNumber,
    string? CarrierCode,
    string? CarrierName,
    DateTimeOffset OccurredAt);
