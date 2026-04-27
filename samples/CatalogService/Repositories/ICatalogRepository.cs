using CatalogService.Models;

namespace CatalogService.Repositories;

public interface ICatalogRepository
{
    Task<List<CatalogItem>> GetAllAsync(CancellationToken cancellationToken);
    Task<CatalogItem?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<CatalogItem> UpsertAsync(string id, UpsertCatalogItemRequest request, CancellationToken cancellationToken);
}
