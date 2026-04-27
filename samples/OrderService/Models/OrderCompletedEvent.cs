namespace OrderService.Models;

public sealed record OrderCompletedEvent(
    string OrderId,
    string? UserId,
    DateTimeOffset OccurredAt);
