namespace AuditService.Models;

public sealed record AuditEventRequest(string ActorId, string Action, string ResourceType, string ResourceId, string Result, string? Detail);
public sealed record AuditEvent(Guid Id, string ActorId, string Action, string ResourceType, string ResourceId, string Result, string? Detail, DateTimeOffset CreatedAt);
public sealed record OrderCreatedEvent(string OrderId, string ProductId, int Quantity, DateTimeOffset CreatedAt);
