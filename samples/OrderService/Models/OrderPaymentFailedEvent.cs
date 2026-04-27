namespace OrderService.Models;

public sealed record OrderPaymentFailedEvent(
    string OrderId,
    string? UserId,
    string Status,
    string? TransactionId,
    string Reason,
    DateTimeOffset OccurredAt);
