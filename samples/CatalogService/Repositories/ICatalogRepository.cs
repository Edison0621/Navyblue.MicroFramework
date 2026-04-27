using CatalogService.Models;

namespace CatalogService.Repositories;

public interface ICatalogRepository
{
    Task<List<CatalogItem>> GetAllAsync(CancellationToken cancellationToken);
    Task<CatalogItem?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<CatalogItem> UpsertAsync(string id, UpsertCatalogItemRequest request, CancellationToken cancellationToken);
    Task<CatalogItem> SaveAsync(CatalogItem item, CancellationToken cancellationToken);
    Task<List<CatalogCategory>> GetAllCategoriesAsync(CancellationToken cancellationToken);
    Task<CatalogCategory?> GetCategoryByIdAsync(string id, CancellationToken cancellationToken);
    Task<CatalogCategory> UpsertCategoryAsync(string id, UpsertCatalogCategoryRequest request, CancellationToken cancellationToken);
    Task<CatalogCategory> SaveCategoryAsync(CatalogCategory category, CancellationToken cancellationToken);
    Task<List<CatalogItem>> GetItemsByCategoryIdAsync(string categoryId, CancellationToken cancellationToken);
    Task<List<CatalogCategory>> GetChildCategoriesRecursiveAsync(string categoryId, CancellationToken cancellationToken);
    Task<bool> DeleteCategoryAsync(string id, CancellationToken cancellationToken);
}
