namespace OrderService.Models;

public sealed record OrderPaidEvent(string OrderId, string? UserId, DateTimeOffset OccurredAt);
