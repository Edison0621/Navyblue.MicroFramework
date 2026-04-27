using Dapr.Client;
using InventoryService.Models;

namespace InventoryService.Repositories;

public sealed class DaprInventoryRepository(DaprClient daprClient) : IInventoryRepository
{
    private const string StateStoreName = "statestore";

    public async Task<InventoryStock?> GetAsync(string productId, CancellationToken cancellationToken)
    {
        return await daprClient.GetStateAsync<InventoryStock>(StateStoreName, BuildStockKey(productId), cancellationToken: cancellationToken);
    }

    public async Task<InventoryStock> SetAsync(string productId, int quantity, CancellationToken cancellationToken)
    {
        var stock = new InventoryStock(productId, quantity, DateTimeOffset.UtcNow);
        await daprClient.SaveStateAsync(StateStoreName, BuildStockKey(productId), stock, cancellationToken: cancellationToken);
        return stock;
    }

    public async Task<InventoryReservationBucket> GetReservationBucketAsync(string productId, CancellationToken cancellationToken)
    {
        return await daprClient.GetStateAsync<InventoryReservationBucket>(StateStoreName, BuildReservationBucketKey(productId), cancellationToken: cancellationToken)
            ?? new InventoryReservationBucket { ProductId = productId };
    }

    public async Task SaveReservationBucketAsync(string productId, InventoryReservationBucket bucket, CancellationToken cancellationToken)
    {
        await daprClient.SaveStateAsync(StateStoreName, BuildReservationBucketKey(productId), bucket, cancellationToken: cancellationToken);
    }

    public async Task<InventoryReservationLedger> GetReservationLedgerAsync(CancellationToken cancellationToken)
    {
        return await daprClient.GetStateAsync<InventoryReservationLedger>(StateStoreName, InventoryReservationLedger.ProductIndexStateKey, cancellationToken: cancellationToken)
            ?? new InventoryReservationLedger();
    }

    public async Task SaveReservationLedgerAsync(InventoryReservationLedger ledger, CancellationToken cancellationToken)
    {
        await daprClient.SaveStateAsync(StateStoreName, InventoryReservationLedger.ProductIndexStateKey, ledger, cancellationToken: cancellationToken);
    }

    private static string BuildStockKey(string productId) => $"inventory:stock:{productId}";
    private static string BuildReservationBucketKey(string productId) => $"inventory:reservation:{productId}";
}
