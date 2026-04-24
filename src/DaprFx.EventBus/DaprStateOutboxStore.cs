using Dapr.Client;
using DaprFx.Core;

namespace DaprFx.EventBus;

internal sealed class DaprStateOutboxStore(DaprClient daprClient, DaprFxOptions options) : IOutboxStore
{
    private readonly DaprClient _daprClient = daprClient;
    private readonly DaprFxOptions _options = options;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        return UpdateQueueAsync(list =>
        {
            list.Add(message);
            return list;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> DequeueBatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var current = await _daprClient.GetStateAsync<List<OutboxMessage>>(_options.StateStoreName, _options.OutboxStateKey, cancellationToken: cancellationToken) ?? [];
            var now = DateTimeOffset.UtcNow;
            var due = current
                .Where(message => message.NextVisibleAt is null || message.NextVisibleAt <= now)
                .OrderBy(message => message.CreatedAt)
                .Take(batchSize)
                .ToList();
            var batchIds = due.Select(x => x.Id).ToHashSet();
            current = current.Where(message => !batchIds.Contains(message.Id)).ToList();
            await _daprClient.SaveStateAsync(_options.StateStoreName, _options.OutboxStateKey, current, cancellationToken: cancellationToken);
            return due;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task UpdateQueueAsync(Func<List<OutboxMessage>, List<OutboxMessage>> update, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var current = await _daprClient.GetStateAsync<List<OutboxMessage>>(_options.StateStoreName, _options.OutboxStateKey, cancellationToken: cancellationToken) ?? [];
            var next = update(current);
            await _daprClient.SaveStateAsync(_options.StateStoreName, _options.OutboxStateKey, next, cancellationToken: cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }
}
