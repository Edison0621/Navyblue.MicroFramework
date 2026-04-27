namespace OrderService.Models;

public sealed record MerchantAfterSaleView(
    string OrderId,
    string ShopId,
    string SubOrderId,
    string AfterSaleId,
    string Status,
    string Reason,
    string? Detail,
    decimal? RequestedAmount,
    string RequestedByUserId,
    DateTimeOffset RequestedAt,
    string? RefundStatus,
    decimal? RefundedAmount,
    DateTimeOffset? RefundedAt);
