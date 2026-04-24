namespace DaprFx.Core;

public sealed record OutboxMessage(
    Guid Id,
    string Topic,
    object Payload,
    DateTimeOffset CreatedAt,
    int AttemptCount = 0,
    DateTimeOffset? NextVisibleAt = null,
    string? LastError = null);
