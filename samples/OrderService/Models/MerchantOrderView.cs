namespace OrderService.Models;

public sealed record MerchantOrderView(
    string OrderId,
    string ShopId,
    string? UserId,
    string OrderStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    decimal ShopSubtotal,
    string SubOrderId,
    string SubOrderStatus,
    IReadOnlyList<OrderLine> Lines);
