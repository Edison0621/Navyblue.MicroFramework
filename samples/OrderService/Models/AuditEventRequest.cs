namespace OrderService.Models;

public sealed record AuditEventRequest(
    string ActorId,
    string Action,
    string ResourceType,
    string ResourceId,
    string Result,
    string? Detail);
