namespace OrderService.Models;

public sealed record OrderCreatedEvent(string OrderId, string ProductId, int Quantity, DateTimeOffset CreatedAt);
