using System.Net.Sockets;
using CatalogService.Models;
using Dapr.Client;

namespace CatalogService.Repositories;

public sealed class DaprCatalogRepository(DaprClient daprClient, ILogger<DaprCatalogRepository> logger) : ICatalogRepository
{
    private const string StateStoreName = "statestore";
    private const string CatalogIndexStateKey = "catalog:index";
    private const string CategoryIndexStateKey = "catalog:category:index";
    private const string CategoryItemIndexPrefix = "catalog:category:items:";
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(120),
        TimeSpan.FromMilliseconds(280),
        TimeSpan.FromMilliseconds(550)
    ];

    public async Task<List<CatalogItem>> GetAllAsync(CancellationToken cancellationToken)
    {
        var ids = await GetStateAsync<HashSet<string>>(CatalogIndexStateKey, cancellationToken) ?? [];
        var items = new List<CatalogItem>();
        foreach (var id in ids)
        {
            var item = await GetStateAsync<CatalogItem>(BuildItemKey(id), cancellationToken);
            if (item is not null)
            {
                items.Add(NormalizeLegacyItem(item));
            }
        }

        return items.OrderBy(x => x.Name).ToList();
    }

    public async Task<CatalogItem?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var item = await GetStateAsync<CatalogItem>(BuildItemKey(id), cancellationToken);
        return item is null ? null : NormalizeLegacyItem(item);
    }

    public async Task<CatalogItem> UpsertAsync(string id, UpsertCatalogItemRequest request, CancellationToken cancellationToken)
    {
        var existing = await GetByIdAsync(id, cancellationToken);
        var shopId = string.IsNullOrWhiteSpace(request.ShopId) ? "shop-default" : request.ShopId.Trim();
        var categoryId = string.IsNullOrWhiteSpace(request.CategoryId) ? null : request.CategoryId.Trim();
        var normalizedSkus = (request.Skus ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.SkuId))
            .GroupBy(x => x.SkuId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.First();
                var name = string.IsNullOrWhiteSpace(first.Name) ? g.Key : first.Name.Trim();
                var price = first.Price ?? request.Price;
                return new CatalogSku(g.Key, name, Math.Round(price, 2), first.IsActive);
            })
            .OrderBy(x => x.SkuId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var item = existing ?? new CatalogItem { Id = id };
        item.Name = request.Name;
        item.Price = request.Price;
        item.IsActive = request.IsActive;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.ShopId = shopId;
        item.CategoryId = categoryId;
        item.CategoryEnabled = true;
        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            var category = await GetCategoryByIdAsync(categoryId, cancellationToken);
            item.CategoryEnabled = category?.IsEnabled ?? true;
        }
        item.Skus = normalizedSkus;
        item.AuditStatus ??= CatalogAuditStatus.Draft;
        item.IsOnShelf = item.IsOnShelf && item.IsActive;
        await SaveAsync(item, cancellationToken);

        var ids = await GetStateAsync<HashSet<string>>(CatalogIndexStateKey, cancellationToken) ?? [];
        if (ids.Add(id))
        {
            await SaveStateAsync(CatalogIndexStateKey, ids, cancellationToken);
        }

        return item;
    }

    public async Task<CatalogItem> SaveAsync(CatalogItem item, CancellationToken cancellationToken)
    {
        var existing = await GetByIdAsync(item.Id, cancellationToken);
        item = NormalizeLegacyItem(item);
        await SaveStateAsync(BuildItemKey(item.Id), item, cancellationToken);
        if (!string.Equals(existing?.CategoryId, item.CategoryId, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(existing?.CategoryId))
            {
                await RemoveItemFromCategoryIndexAsync(existing.CategoryId!, item.Id, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(item.CategoryId))
            {
                await AddItemToCategoryIndexAsync(item.CategoryId!, item.Id, cancellationToken);
            }
        }

        return item;
    }

    public async Task<List<CatalogCategory>> GetAllCategoriesAsync(CancellationToken cancellationToken)
    {
        var ids = await GetStateAsync<HashSet<string>>(CategoryIndexStateKey, cancellationToken) ?? [];
        var list = new List<CatalogCategory>();
        foreach (var id in ids)
        {
            var category = await GetCategoryByIdAsync(id, cancellationToken);
            if (category is not null)
            {
                list.Add(category);
            }
        }

        return list
            .OrderBy(x => x.ParentId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<CatalogCategory?> GetCategoryByIdAsync(string id, CancellationToken cancellationToken)
    {
        return await GetStateAsync<CatalogCategory>(BuildCategoryKey(id), cancellationToken);
    }

    public async Task<CatalogCategory> UpsertCategoryAsync(string id, UpsertCatalogCategoryRequest request, CancellationToken cancellationToken)
    {
        var existing = await GetCategoryByIdAsync(id, cancellationToken);
        var category = existing ?? new CatalogCategory { Id = id };
        category.Name = request.Name;
        category.ParentId = string.IsNullOrWhiteSpace(request.ParentId) ? null : request.ParentId.Trim();
        category.SortOrder = request.SortOrder;
        category.IsVisible = request.IsVisible;
        category.IsEnabled = request.IsEnabled;
        category.UpdatedAt = DateTimeOffset.UtcNow;
        return await SaveCategoryAsync(category, cancellationToken);
    }

    public async Task<CatalogCategory> SaveCategoryAsync(CatalogCategory category, CancellationToken cancellationToken)
    {
        await SaveStateAsync(BuildCategoryKey(category.Id), category, cancellationToken);
        var ids = await GetStateAsync<HashSet<string>>(CategoryIndexStateKey, cancellationToken) ?? [];
        if (ids.Add(category.Id))
        {
            await SaveStateAsync(CategoryIndexStateKey, ids, cancellationToken);
        }

        return category;
    }

    public async Task<List<CatalogItem>> GetItemsByCategoryIdAsync(string categoryId, CancellationToken cancellationToken)
    {
        var ids = await GetStateAsync<HashSet<string>>(BuildCategoryItemIndexKey(categoryId), cancellationToken) ?? [];
        var list = new List<CatalogItem>();
        foreach (var id in ids)
        {
            var item = await GetByIdAsync(id, cancellationToken);
            if (item is not null)
            {
                list.Add(item);
            }
        }

        return list;
    }

    public async Task<List<CatalogCategory>> GetChildCategoriesRecursiveAsync(string categoryId, CancellationToken cancellationToken)
    {
        var categories = await GetAllCategoriesAsync(cancellationToken);
        var byParent = categories
            .Where(x => !string.IsNullOrWhiteSpace(x.ParentId))
            .GroupBy(x => x.ParentId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var result = new List<CatalogCategory>();
        var queue = new Queue<string>();
        queue.Enqueue(categoryId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!byParent.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                result.Add(child);
                queue.Enqueue(child.Id);
            }
        }

        return result;
    }

    public async Task<bool> DeleteCategoryAsync(string id, CancellationToken cancellationToken)
    {
        var existing = await GetCategoryByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        var ids = await GetStateAsync<HashSet<string>>(CategoryIndexStateKey, cancellationToken) ?? [];
        ids.Remove(id);
        await SaveStateAsync(CategoryIndexStateKey, ids, cancellationToken);
        await DeleteStateAsync(BuildCategoryKey(id), cancellationToken);
        return true;
    }

    private static string BuildItemKey(string id) => $"catalog:item:{id}";
    private static string BuildCategoryKey(string id) => $"catalog:category:{id}";
    private static string BuildCategoryItemIndexKey(string categoryId) => $"{CategoryItemIndexPrefix}{categoryId}";

    private async Task AddItemToCategoryIndexAsync(string categoryId, string itemId, CancellationToken cancellationToken)
    {
        var key = BuildCategoryItemIndexKey(categoryId);
        var ids = await GetStateAsync<HashSet<string>>(key, cancellationToken) ?? [];
        if (ids.Add(itemId))
        {
            await SaveStateAsync(key, ids, cancellationToken);
        }
    }

    private async Task RemoveItemFromCategoryIndexAsync(string categoryId, string itemId, CancellationToken cancellationToken)
    {
        var key = BuildCategoryItemIndexKey(categoryId);
        var ids = await GetStateAsync<HashSet<string>>(key, cancellationToken) ?? [];
        if (ids.Remove(itemId))
        {
            await SaveStateAsync(key, ids, cancellationToken);
        }
    }

    private static CatalogItem NormalizeLegacyItem(CatalogItem item)
    {
        item.ShopId = string.IsNullOrWhiteSpace(item.ShopId) ? "shop-default" : item.ShopId.Trim();
        if (string.IsNullOrWhiteSpace(item.AuditStatus))
        {
            item.AuditStatus = item.IsActive ? CatalogAuditStatus.Approved : CatalogAuditStatus.Draft;
        }

        if (!item.IsOnShelf && item.IsActive && string.Equals(item.AuditStatus, CatalogAuditStatus.Approved, StringComparison.OrdinalIgnoreCase))
        {
            // Legacy records had only IsActive. Treat them as already on shelf.
            item.IsOnShelf = true;
        }

        item.Skus ??= [];
        item.AuditHistory ??= [];
        if (item.CategoryId is null)
        {
            item.CategoryEnabled = true;
        }
        return item;
    }

    private Task<T> GetStateAsync<T>(string key, CancellationToken cancellationToken)
    {
        return ExecuteWithRetryAsync(
            ct => daprClient.GetStateAsync<T>(StateStoreName, key, cancellationToken: ct),
            $"GetState:{key}",
            cancellationToken);
    }

    private Task SaveStateAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        return ExecuteWithRetryAsync(
            ct => daprClient.SaveStateAsync(StateStoreName, key, value, cancellationToken: ct),
            $"SaveState:{key}",
            cancellationToken);
    }

    private Task DeleteStateAsync(string key, CancellationToken cancellationToken)
    {
        return ExecuteWithRetryAsync(
            ct => daprClient.DeleteStateAsync(StateStoreName, key, cancellationToken: ct),
            $"DeleteState:{key}",
            cancellationToken);
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
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
                logger.LogWarning(ex, "Catalog state operation {OperationName} failed at attempt {Attempt}", operationName, attempt + 1);
                if (attempt < RetryDelays.Length)
                {
                    await Task.Delay(RetryDelays[attempt], cancellationToken);
                }
            }
        }

        throw new InvalidOperationException($"Catalog state operation {operationName} failed after retries.", lastError);
    }

    private async Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        await ExecuteWithRetryAsync<object?>(
            async ct =>
            {
                await operation(ct);
                return null;
            },
            operationName,
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
