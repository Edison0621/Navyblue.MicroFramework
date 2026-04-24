using Dapr.Client;
using DaprFx.Core;

namespace DaprFx.StateManagement;

public sealed class DaprStateStore<T>(DaprClient client, string storeName) : IStateStore<T> where T : class
{
    private readonly DaprClient _client = client;
    private readonly string _storeName = storeName;

    public async Task<T?> GetAsync(string key, CancellationToken ct = default)
        => await _client.GetStateAsync<T>(_storeName, key, cancellationToken: ct);

    public async Task SaveAsync(string key, T value, CancellationToken ct = default)
        => await _client.SaveStateAsync(_storeName, key, value, cancellationToken: ct);

    public async Task DeleteAsync(string key, CancellationToken ct = default)
        => await _client.DeleteStateAsync(_storeName, key, cancellationToken: ct);
}
