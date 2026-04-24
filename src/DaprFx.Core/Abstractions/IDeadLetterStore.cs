namespace DaprFx.Core;

public interface IDeadLetterStore
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> ListAsync(int take = 100, CancellationToken cancellationToken = default);
    Task RequeueAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid messageId, CancellationToken cancellationToken = default);
}
