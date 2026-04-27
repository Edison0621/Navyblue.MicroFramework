namespace OrderService.Models;

public sealed class OrderPayIdempotencyRecord
{
    public string OrderId { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}
