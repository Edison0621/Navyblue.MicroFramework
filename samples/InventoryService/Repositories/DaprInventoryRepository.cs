using Dapr.Client;
using InventoryService.Models;

namespace InventoryService.Repositories;

public sealed class DaprInventoryRepository(DaprClient daprClient, ILogger<DaprInventoryRepository> logger) : IInventoryRepository
{
    private const string StateStoreName = "statestore";

    public async Task<InventoryStock?> GetAsync(string productId, CancellationToken cancellationToken)
    {
        return await GetStateAsync<InventoryStock>(BuildStockKey(productId), cancellationToken);
    }

    public async Task<InventoryStock> SetAsync(string productId, int quantity, CancellationToken cancellationToken)
    {
        var stock = new InventoryStock(productId, quantity, DateTimeOffset.UtcNow);
        await SaveStateAsync(BuildStockKey(productId), stock, cancellationToken);
        return stock;
    }

    public async Task<InventoryReservationBucket> GetReservationBucketAsync(string productId, CancellationToken cancellationToken)
    {
        return await GetStateAsync<InventoryReservationBucket>(BuildReservationBucketKey(productId), cancellationToken)
            ?? new InventoryReservationBucket { ProductId = productId };
    }

    public async Task SaveReservationBucketAsync(string productId, InventoryReservationBucket bucket, CancellationToken cancellationToken)
    {
        await SaveStateAsync(BuildReservationBucketKey(productId), bucket, cancellationToken);
    }

    public async Task<InventoryReservationLedger> GetReservationLedgerAsync(CancellationToken cancellationToken)
    {
        return await GetStateAsync<InventoryReservationLedger>(InventoryReservationLedger.ProductIndexStateKey, cancellationToken)
            ?? new InventoryReservationLedger();
    }

    public async Task SaveReservationLedgerAsync(InventoryReservationLedger ledger, CancellationToken cancellationToken)
    {
        await SaveStateAsync(InventoryReservationLedger.ProductIndexStateKey, ledger, cancellationToken);
    }

    private static string BuildStockKey(string productId) => $"inventory:stock:{productId}";
    private static string BuildReservationBucketKey(string productId) => $"inventory:reservation:{productId}";

    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(260), TimeSpan.FromMilliseconds(500)];

    private async Task<T?> GetStateAsync<T>(string key, CancellationToken ct)
    {
        return await ExecuteWithRetryAsync(x => daprClient.GetStateAsync<T>(StateStoreName, key, cancellationToken: x), key, ct);
    }

    private async Task SaveStateAsync<T>(string key, T value, CancellationToken ct)
    {
        await ExecuteWithRetryAsync(x => daprClient.SaveStateAsync(StateStoreName, key, value, cancellationToken: x), key, ct);
    }

    private async Task<TResult> ExecuteWithRetryAsync<TResult>(Func<CancellationToken, Task<TResult>> op, string key, CancellationToken ct)
    {
        Exception? last = null;
        for (var i = 0; i <= RetryDelays.Length; i++)
        {
            try { return await op(ct); }
            catch (Exception ex) when (ex.GetType().FullName?.Contains("DaprException", StringComparison.Ordinal) == true || ex is HttpRequestException or TimeoutException)
            {
                last = ex;
                logger.LogWarning(ex, "Inventory state op failed on {Key}, attempt {Attempt}", key, i + 1);
                if (i < RetryDelays.Length) await Task.Delay(RetryDelays[i], ct);
            }
        }

        throw new InvalidOperationException($"Inventory state operation failed on key {key}.", last);
    }

    private async Task ExecuteWithRetryAsync(Func<CancellationToken, Task> op, string key, CancellationToken ct)
    {
        await ExecuteWithRetryAsync<object?>(
            async x =>
            {
                await op(x);
                return null;
            },
            key,
            ct);
    }
}
