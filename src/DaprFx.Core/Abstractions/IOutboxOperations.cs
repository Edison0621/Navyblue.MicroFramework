namespace DaprFx.Core;

public interface IOutboxOperations
{
    Task<IReadOnlyList<OutboxMessage>> ListDeadLettersAsync(
        int take = 100,
        string? topic = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);
    Task RequeueDeadLetterAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task<int> ReplayAllDeadLettersAsync(bool dryRun = false, string? topic = null, CancellationToken cancellationToken = default);
    Task DeleteDeadLetterAsync(Guid messageId, CancellationToken cancellationToken = default);
}
