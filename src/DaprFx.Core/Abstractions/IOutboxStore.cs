namespace DaprFx.Core;

public interface IOutboxStore
{
    Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> DequeueBatchAsync(int batchSize, CancellationToken cancellationToken = default);
}
