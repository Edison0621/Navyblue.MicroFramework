using DaprFx.Core;

namespace DaprFx.EventBus;

internal sealed class OutboxOperations(IDeadLetterStore deadLetterStore) : IOutboxOperations
{
    private readonly IDeadLetterStore _deadLetterStore = deadLetterStore;

    public async Task<IReadOnlyList<OutboxMessage>> ListDeadLettersAsync(
        int take = 100,
        string? topic = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var items = await _deadLetterStore.ListAsync(take, cancellationToken);
        IEnumerable<OutboxMessage> query = items;

        if (!string.IsNullOrWhiteSpace(topic))
        {
            query = query.Where(message => string.Equals(message.Topic, topic, StringComparison.OrdinalIgnoreCase));
        }

        if (from.HasValue)
        {
            query = query.Where(message => message.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(message => message.CreatedAt <= to.Value);
        }

        return query.Take(take).ToList();
    }

    public Task RequeueDeadLetterAsync(Guid messageId, CancellationToken cancellationToken = default)
        => _deadLetterStore.RequeueAsync(messageId, cancellationToken);

    public async Task<int> ReplayAllDeadLettersAsync(bool dryRun = false, string? topic = null, CancellationToken cancellationToken = default)
    {
        var items = await ListDeadLettersAsync(take: int.MaxValue, topic: topic, cancellationToken: cancellationToken);
        if (dryRun)
        {
            return items.Count;
        }

        foreach (var message in items)
        {
            await _deadLetterStore.RequeueAsync(message.Id, cancellationToken);
        }

        return items.Count;
    }

    public Task DeleteDeadLetterAsync(Guid messageId, CancellationToken cancellationToken = default)
        => _deadLetterStore.DeleteAsync(messageId, cancellationToken);
}
