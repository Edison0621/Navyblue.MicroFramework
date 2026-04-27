using CatalogService.Models;
using Dapr.Client;

namespace CatalogService.Repositories;

public sealed class DaprCatalogRepository(DaprClient daprClient) : ICatalogRepository
{
    private const string StateStoreName = "statestore";
    private const string CatalogIndexStateKey = "catalog:index";

    public async Task<List<CatalogItem>> GetAllAsync(CancellationToken cancellationToken)
    {
        var ids = await daprClient.GetStateAsync<HashSet<string>>(StateStoreName, CatalogIndexStateKey, cancellationToken: cancellationToken) ?? [];
        var items = new List<CatalogItem>();
        foreach (var id in ids)
        {
            var item = await daprClient.GetStateAsync<CatalogItem>(StateStoreName, BuildItemKey(id), cancellationToken: cancellationToken);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        return items.OrderBy(x => x.Name).ToList();
    }

    public async Task<CatalogItem?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return await daprClient.GetStateAsync<CatalogItem>(StateStoreName, BuildItemKey(id), cancellationToken: cancellationToken);
    }

    public async Task<CatalogItem> UpsertAsync(string id, UpsertCatalogItemRequest request, CancellationToken cancellationToken)
    {
        var shopId = string.IsNullOrWhiteSpace(request.ShopId) ? "shop-default" : request.ShopId.Trim();
        var item = new CatalogItem(id, request.Name, request.Price, request.IsActive, DateTimeOffset.UtcNow, shopId);
        await daprClient.SaveStateAsync(StateStoreName, BuildItemKey(id), item, cancellationToken: cancellationToken);

        var ids = await daprClient.GetStateAsync<HashSet<string>>(StateStoreName, CatalogIndexStateKey, cancellationToken: cancellationToken) ?? [];
        if (ids.Add(id))
        {
            await daprClient.SaveStateAsync(StateStoreName, CatalogIndexStateKey, ids, cancellationToken: cancellationToken);
        }

        return item;
    }

    private static string BuildItemKey(string id) => $"catalog:item:{id}";
}
