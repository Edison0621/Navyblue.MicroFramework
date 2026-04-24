using Dapr.Client;
using DaprFx.Core;

namespace DaprFx.EventBus;

internal sealed class DaprStateDeadLetterStore(DaprClient daprClient, DaprFxOptions options) : IDeadLetterStore
{
    private readonly DaprClient _daprClient = daprClient;
    private readonly DaprFxOptions _options = options;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        => EnqueueAsync(message, cancellationToken);

    private async Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var current = await _daprClient.GetStateAsync<List<OutboxMessage>>(_options.StateStoreName, _options.OutboxDeadLetterStateKey, cancellationToken: cancellationToken) ?? [];
            current.Add(message);
            await _daprClient.SaveStateAsync(_options.StateStoreName, _options.OutboxDeadLetterStateKey, current, cancellationToken: cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<OutboxMessage>> ListAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        var current = await _daprClient.GetStateAsync<List<OutboxMessage>>(_options.StateStoreName, _options.OutboxDeadLetterStateKey, cancellationToken: cancellationToken) ?? [];
        return current.OrderByDescending(x => x.CreatedAt).Take(take).ToList();
    }

    public async Task RequeueAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var dead = await _daprClient.GetStateAsync<List<OutboxMessage>>(_options.StateStoreName, _options.OutboxDeadLetterStateKey, cancellationToken: cancellationToken) ?? [];
            var message = dead.FirstOrDefault(x => x.Id == messageId);
            if (message is null)
            {
                return;
            }

            dead.RemoveAll(x => x.Id == messageId);
            await _daprClient.SaveStateAsync(_options.StateStoreName, _options.OutboxDeadLetterStateKey, dead, cancellationToken: cancellationToken);

            var outbox = await _daprClient.GetStateAsync<List<OutboxMessage>>(_options.StateStoreName, _options.OutboxStateKey, cancellationToken: cancellationToken) ?? [];
            outbox.Add(message with { AttemptCount = 0, LastError = null, NextVisibleAt = null });
            await _daprClient.SaveStateAsync(_options.StateStoreName, _options.OutboxStateKey, outbox, cancellationToken: cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task DeleteAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var dead = await _daprClient.GetStateAsync<List<OutboxMessage>>(_options.StateStoreName, _options.OutboxDeadLetterStateKey, cancellationToken: cancellationToken) ?? [];
            dead.RemoveAll(x => x.Id == messageId);
            await _daprClient.SaveStateAsync(_options.StateStoreName, _options.OutboxDeadLetterStateKey, dead, cancellationToken: cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }
}
