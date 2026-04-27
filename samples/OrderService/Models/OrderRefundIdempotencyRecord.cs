namespace OrderService.Models;

public sealed class OrderRefundIdempotencyRecord
{
    public string OrderId { get; set; } = string.Empty;
    public string AfterSaleId { get; set; } = string.Empty;
    public string RefundTransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
