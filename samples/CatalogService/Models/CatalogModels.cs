namespace CatalogService.Models;

public sealed record UpsertCatalogItemRequest(
    string Name,
    decimal Price,
    bool IsActive,
    string? ShopId = null,
    IReadOnlyList<UpsertCatalogSkuRequest>? Skus = null);

public sealed record UpsertCatalogSkuRequest(
    string SkuId,
    string Name,
    decimal? Price = null,
    bool IsActive = true);

public sealed record CatalogItem(
    string Id,
    string Name,
    decimal Price,
    bool IsActive,
    DateTimeOffset UpdatedAt,
    string? ShopId = null,
    IReadOnlyList<CatalogSku>? Skus = null);

public sealed record CatalogSku(
    string SkuId,
    string Name,
    decimal Price,
    bool IsActive);
