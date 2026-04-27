namespace CatalogService.Models;

public sealed record UpsertCatalogItemRequest(string Name, decimal Price, bool IsActive);
public sealed record CatalogItem(string Id, string Name, decimal Price, bool IsActive, DateTimeOffset UpdatedAt);
