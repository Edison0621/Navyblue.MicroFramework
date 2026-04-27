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

    private static string BuildStockKey(string productId) => $"inventory:stock:{productId}";
}
