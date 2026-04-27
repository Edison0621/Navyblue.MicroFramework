namespace NotificationService.Models;

public sealed record SendNotificationRequest(string Channel, string To, string Title, string Body);
public sealed record NotificationItem(string Id, string Channel, string To, string Title, string Body, DateTimeOffset CreatedAt);
public sealed record OrderCreatedEvent(string OrderId, string ProductId, int Quantity, DateTimeOffset CreatedAt);
public sealed record OrderCompensationFailedEvent(string OrderId, string ProductId, int Quantity, string Error, DateTimeOffset OccurredAt);
