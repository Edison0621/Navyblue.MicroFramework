namespace OrderService.Models;

public sealed record OrderCreatedEvent(
    string OrderId,
    string ProductId,
    int Quantity,
    DateTimeOffset CreatedAt,
    string? UserId = null,
    IReadOnlyList<OrderCreatedSubOrderPayload>? SubOrders = null);

public sealed record OrderCreatedSubOrderPayload(string ShopId, string SubOrderId, IReadOnlyList<OrderCreatedLinePayload> Lines);

public sealed record OrderCreatedLinePayload(string ProductId, string? SkuId, int Quantity, decimal UnitPrice);
