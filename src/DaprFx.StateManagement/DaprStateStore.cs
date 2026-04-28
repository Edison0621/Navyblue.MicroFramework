using System.Net.Sockets;
using Dapr.Client;
using DaprFx.Core;

namespace DaprFx.StateManagement;

public sealed class DaprStateStore<T>(DaprClient client, string storeName) : IStateStore<T> where T : class
{
    private readonly DaprClient _client = client;
    private readonly string _storeName = storeName;
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(120),
        TimeSpan.FromMilliseconds(260),
        TimeSpan.FromMilliseconds(500)
    ];

    public async Task<T?> GetAsync(string key, CancellationToken ct = default)
        => await ExecuteWithRetryAsync(x => _client.GetStateAsync<T>(_storeName, key, cancellationToken: x), ct);

    public async Task SaveAsync(string key, T value, CancellationToken ct = default)
        => await ExecuteWithRetryAsync(x => _client.SaveStateAsync(_storeName, key, value, cancellationToken: x), ct);

    public async Task DeleteAsync(string key, CancellationToken ct = default)
        => await ExecuteWithRetryAsync(x => _client.DeleteStateAsync(_storeName, key, cancellationToken: x), ct);

    private static async Task<TResult> ExecuteWithRetryAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                lastError = ex;
                if (attempt < RetryDelays.Length)
                {
                    await Task.Delay(RetryDelays[attempt], cancellationToken);
                }
            }
        }

        throw new InvalidOperationException("Dapr state operation failed after retries.", lastError);
    }

    private static async Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        await ExecuteWithRetryAsync<object?>(
            async ct =>
            {
                await operation(ct);
                return null;
            },
            cancellationToken);
    }

    private static bool IsTransient(Exception ex)
    {
        if (ex.GetType().FullName?.Contains("DaprException", StringComparison.Ordinal) == true)
        {
            return true;
        }

        if (ex is HttpRequestException or TimeoutException or SocketException)
        {
            return true;
        }

        return ex.InnerException is not null && IsTransient(ex.InnerException);
    }
}
