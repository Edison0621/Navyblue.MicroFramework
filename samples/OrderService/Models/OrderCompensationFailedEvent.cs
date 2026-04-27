namespace OrderService.Models;

public sealed record OrderCompensationFailedEvent(
    string OrderId,
    string ProductId,
    int Quantity,
    string Error,
    DateTimeOffset OccurredAt);
