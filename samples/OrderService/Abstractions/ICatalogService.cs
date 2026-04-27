using DaprFx.Core;
using OrderService.Models;

namespace OrderService.Abstractions;

public interface ICatalogService
{
    [DaprInvoke("/api/catalog/items/{id}")]
    Task<ApiResponse<CatalogItemDto>?> GetItemAsync(string id);
}
