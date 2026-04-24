using Dapr.Client;
using DaprFx.Core;

namespace DaprFx.EventBus;

internal sealed class DaprStateIdempotencyStore(DaprClient daprClient, DaprFxOptions options) : IIdempotencyStore
{
    private readonly DaprClient _daprClient = daprClient;
    private readonly DaprFxOptions _options = options;

    public async Task<bool> TryBeginAsync(string key, CancellationToken cancellationToken = default)
    {
        var stateKey = $"{_options.IdempotencyStateKeyPrefix}{key}";
        var existing = await _daprClient.GetStateAsync<IdempotencyRecord?>(_options.StateStoreName, stateKey, cancellationToken: cancellationToken);
        if (existing is not null && existing.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return false;
        }

        var ttl = TimeSpan.FromMinutes(Math.Max(1, _options.IdempotencyTtlMinutes));
        var record = new IdempotencyRecord(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.Add(ttl));
        await _daprClient.SaveStateAsync(_options.StateStoreName, stateKey, record, cancellationToken: cancellationToken);
        return true;
    }

    public Task CompleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

    private sealed record IdempotencyRecord(DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
}
