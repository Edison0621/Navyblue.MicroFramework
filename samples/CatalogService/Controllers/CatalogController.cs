using CatalogService.Models;
using CatalogService.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/catalog")]
public sealed class CatalogController(ICatalogRepository catalogRepository) : ControllerBase
{
    [HttpGet("items")]
    public async Task<IActionResult> GetItems(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? q,
        [FromQuery] string? shopId,
        [FromQuery] string? skuId,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var items = await catalogRepository.GetAllAsync(cancellationToken);
        var query = new PageQuery(page, pageSize);
        IEnumerable<CatalogItem> filtered = items;
        if (!string.IsNullOrWhiteSpace(q))
        {
            var kw = q.Trim();
            filtered = filtered.Where(x =>
                x.Name.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || x.Id.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || (x.Skus ?? []).Any(s => s.Name.Contains(kw, StringComparison.OrdinalIgnoreCase) || s.SkuId.Contains(kw, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(shopId))
        {
            var sid = shopId.Trim();
            filtered = filtered.Where(x => string.Equals(x.ShopId, sid, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(skuId))
        {
            var sku = skuId.Trim();
            filtered = filtered.Where(x => (x.Skus ?? []).Any(s => string.Equals(s.SkuId, sku, StringComparison.OrdinalIgnoreCase)));
        }

        if (isActive.HasValue)
        {
            filtered = filtered.Where(x => x.IsActive == isActive.Value);
        }

        var ordered = filtered.ToList();
        var total = ordered.Count;
        var pagedItems = ordered.Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize).ToList();
        var data = new PagedResult<CatalogItem>(pagedItems, query.SafePage, query.SafePageSize, total);
        return Ok(new ApiResponse<PagedResult<CatalogItem>>(true, data, null));
    }

    [HttpGet("items/{id}")]
    public async Task<IActionResult> GetItemById(string id, CancellationToken cancellationToken)
    {
        var item = await catalogRepository.GetByIdAsync(id, cancellationToken);
        return item is null
            ? NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Catalog item not found.")))
            : Ok(new ApiResponse<CatalogItem>(true, item, null));
    }

    [HttpPut("items/{id}")]
    public async Task<IActionResult> UpsertItem(string id, [FromBody] UpsertCatalogItemRequest request, CancellationToken cancellationToken)
    {
        var item = await catalogRepository.UpsertAsync(id, request, cancellationToken);
        return Ok(new ApiResponse<CatalogItem>(true, item, null));
    }
}
