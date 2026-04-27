namespace OrderService.Models;

public sealed class CreateAfterSaleRequest
{
    public string? SubOrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public decimal? RequestedAmount { get; set; }
}

public sealed class ReviewAfterSaleRequest
{
    public string? Note { get; set; }
}

public sealed record AfterSaleRequestedEvent(
    string OrderId,
    string AfterSaleId,
    string UserId,
    string? SubOrderId,
    string Reason,
    decimal? RequestedAmount,
    DateTimeOffset RequestedAt);

public sealed record AfterSaleReviewedEvent(
    string OrderId,
    string AfterSaleId,
    string Status,
    string ReviewedByUserId,
    DateTimeOffset ReviewedAt,
    string? Note);

public sealed record OrderRefundedEvent(
    string OrderId,
    string AfterSaleId,
    string? UserId,
    decimal Amount,
    string RefundTransactionId,
    DateTimeOffset RefundedAt);
