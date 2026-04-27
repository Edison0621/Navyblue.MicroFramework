using CatalogService.Models;
using CatalogService.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/catalog")]
[Authorize]
public sealed class CatalogController(ICatalogRepository catalogRepository) : ControllerBase
{
    [HttpGet("items")]
    public async Task<IActionResult> GetItems(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? q,
        [FromQuery] string? shopId,
        [FromQuery] string? categoryId,
        [FromQuery] string? skuId,
        [FromQuery] string? auditStatus,
        [FromQuery] bool? isOnShelf,
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

        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            var cid = categoryId.Trim();
            filtered = filtered.Where(x => string.Equals(x.CategoryId, cid, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(auditStatus))
        {
            var status = auditStatus.Trim();
            filtered = filtered.Where(x => string.Equals(x.AuditStatus, status, StringComparison.OrdinalIgnoreCase));
        }

        if (isOnShelf.HasValue)
        {
            filtered = filtered.Where(x => x.IsOnShelf == isOnShelf.Value);
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
        if (!CanWriteShop(request.ShopId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Forbidden, "No permission for target shop.")));
        }

        var item = await catalogRepository.UpsertAsync(id, request, cancellationToken);
        return Ok(new ApiResponse<CatalogItem>(true, item, null));
    }

    [HttpPost("items/{id}/submit")]
    public async Task<IActionResult> SubmitForReview(string id, [FromBody] SubmitCatalogItemForReviewRequest? request, CancellationToken cancellationToken)
    {
        var item = await catalogRepository.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Catalog item not found.")));
        }

        if (!CanWriteShop(item.ShopId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Forbidden, "No permission for target shop.")));
        }

        if (item.AuditStatus == CatalogAuditStatus.PendingReview)
        {
            return Ok(new ApiResponse<CatalogItem>(true, item, null));
        }

        item.AuditStatus = CatalogAuditStatus.PendingReview;
        item.AuditReason = null;
        item.LastSubmittedAt = DateTimeOffset.UtcNow;
        item.AuditHistory.Insert(0, new CatalogAuditHistoryEntry
        {
            Action = "submitted",
            Note = request?.Note,
            OccurredAt = DateTimeOffset.UtcNow
        });
        item.UpdatedAt = DateTimeOffset.UtcNow;
        var saved = await catalogRepository.SaveAsync(item, cancellationToken);
        return Ok(new ApiResponse<CatalogItem>(true, saved, null));
    }

    [HttpPost("items/{id}/approve")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ApproveItem(string id, [FromBody] ApproveCatalogItemRequest? request, CancellationToken cancellationToken)
    {
        var item = await catalogRepository.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Catalog item not found.")));
        }

        item.AuditStatus = CatalogAuditStatus.Approved;
        item.AuditReason = null;
        item.LastApprovedAt = DateTimeOffset.UtcNow;
        item.AuditHistory.Insert(0, new CatalogAuditHistoryEntry
        {
            Action = "approved",
            Note = request?.Note,
            OccurredAt = DateTimeOffset.UtcNow
        });
        item.UpdatedAt = DateTimeOffset.UtcNow;
        var saved = await catalogRepository.SaveAsync(item, cancellationToken);
        return Ok(new ApiResponse<CatalogItem>(true, saved, null));
    }

    [HttpPost("items/{id}/reject")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RejectItem(string id, [FromBody] RejectCatalogItemRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidRequest, "Reason is required.")));
        }

        var item = await catalogRepository.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Catalog item not found.")));
        }

        item.AuditStatus = CatalogAuditStatus.Rejected;
        item.AuditReason = request.Reason.Trim();
        item.IsOnShelf = false;
        item.LastRejectedAt = DateTimeOffset.UtcNow;
        item.AuditHistory.Insert(0, new CatalogAuditHistoryEntry
        {
            Action = "rejected",
            Reason = request.Reason.Trim(),
            Note = request.Note,
            OccurredAt = DateTimeOffset.UtcNow
        });
        item.UpdatedAt = DateTimeOffset.UtcNow;
        var saved = await catalogRepository.SaveAsync(item, cancellationToken);
        return Ok(new ApiResponse<CatalogItem>(true, saved, null));
    }

    [HttpGet("items/{id}/audit-history")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetAuditHistory(string id, CancellationToken cancellationToken)
    {
        var item = await catalogRepository.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Catalog item not found.")));
        }

        return Ok(new ApiResponse<IReadOnlyList<CatalogAuditHistoryEntry>>(true, item.AuditHistory, null));
    }

    [HttpPost("items/{id}/shelf")]
    public async Task<IActionResult> SetShelf(string id, [FromBody] SetCatalogItemShelfRequest request, CancellationToken cancellationToken)
    {
        var item = await catalogRepository.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Catalog item not found.")));
        }

        if (!CanWriteShop(item.ShopId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Forbidden, "No permission for target shop.")));
        }

        if (request.IsOnShelf)
        {
            if (item.AuditStatus != CatalogAuditStatus.Approved)
            {
                return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidCatalogState, "Item must be approved before shelf on.", new { item.AuditStatus })));
            }

            if (!item.IsActive)
            {
                return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidCatalogState, "Inactive item cannot be put on shelf.")));
            }

            if (!item.CategoryEnabled)
            {
                return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidCatalogState, "Category is disabled; item cannot be on shelf.", new { item.CategoryId })));
            }

            if (!string.IsNullOrWhiteSpace(item.CategoryId))
            {
                var category = await catalogRepository.GetCategoryByIdAsync(item.CategoryId, cancellationToken);
                if (category is { IsEnabled: false })
                {
                    return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidCatalogState, "Category is disabled; item cannot be on shelf.", new { item.CategoryId })));
                }
            }
        }

        item.IsOnShelf = request.IsOnShelf;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.AuditHistory.Insert(0, new CatalogAuditHistoryEntry
        {
            Action = request.IsOnShelf ? "shelf_on" : "shelf_off",
            OccurredAt = DateTimeOffset.UtcNow
        });
        var saved = await catalogRepository.SaveAsync(item, cancellationToken);
        return Ok(new ApiResponse<CatalogItem>(true, saved, null));
    }

    [HttpPost("items/{id}/shelf-schedule")]
    public async Task<IActionResult> SetShelfSchedule(string id, [FromBody] SetCatalogItemShelfScheduleRequest request, CancellationToken cancellationToken)
    {
        var item = await catalogRepository.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Catalog item not found.")));
        }

        if (!CanWriteShop(item.ShopId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Forbidden, "No permission for target shop.")));
        }

        if (request.OnAt.HasValue && request.OffAt.HasValue && request.OnAt > request.OffAt)
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidRequest, "OnAt must be earlier than OffAt.")));
        }

        item.ScheduledOnAt = request.OnAt;
        item.ScheduledOffAt = request.OffAt;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.AuditHistory.Insert(0, new CatalogAuditHistoryEntry
        {
            Action = "shelf_schedule_set",
            Note = $"on={request.OnAt?.ToString("O") ?? "-"},off={request.OffAt?.ToString("O") ?? "-"}",
            OccurredAt = DateTimeOffset.UtcNow
        });
        var saved = await catalogRepository.SaveAsync(item, cancellationToken);
        return Ok(new ApiResponse<CatalogItem>(true, saved, null));
    }

    [HttpPost("ops/apply-shelf-schedules")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ApplyShelfSchedules([FromQuery] int take = 200, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var safeTake = Math.Clamp(take, 1, 2000);
        var items = await catalogRepository.GetAllAsync(cancellationToken);
        var changed = 0;
        foreach (var item in items.Take(safeTake))
        {
            var shouldOn = item.ScheduledOnAt.HasValue && item.ScheduledOnAt <= now;
            var shouldOff = item.ScheduledOffAt.HasValue && item.ScheduledOffAt <= now;
            if (!shouldOn && !shouldOff)
            {
                continue;
            }

            var changedFlag = false;
            if (shouldOn && !item.IsOnShelf && item.AuditStatus == CatalogAuditStatus.Approved && item.IsActive)
            {
                item.IsOnShelf = true;
                item.ScheduledOnAt = null;
                changedFlag = true;
            }

            if (shouldOff && item.IsOnShelf)
            {
                item.IsOnShelf = false;
                item.ScheduledOffAt = null;
                changedFlag = true;
            }

            if (changedFlag)
            {
                item.UpdatedAt = now;
                await catalogRepository.SaveAsync(item, cancellationToken);
                changed++;
            }
        }

        return Ok(new ApiResponse<object>(true, new { applied = changed }, null));
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await catalogRepository.GetAllCategoriesAsync(cancellationToken);
        var nodes = categories.ToDictionary(
            x => x.Id,
            x => new CatalogCategoryTreeNode
            {
                Id = x.Id,
                Name = x.Name,
                ParentId = x.ParentId,
                SortOrder = x.SortOrder,
                IsVisible = x.IsVisible,
                IsEnabled = x.IsEnabled
            },
            StringComparer.OrdinalIgnoreCase);
        var roots = new List<CatalogCategoryTreeNode>();
        foreach (var category in categories)
        {
            var node = nodes[category.Id];
            if (!string.IsNullOrWhiteSpace(category.ParentId) && nodes.TryGetValue(category.ParentId, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        SortCategoryNodes(roots);
        return Ok(new ApiResponse<IReadOnlyList<CatalogCategoryTreeNode>>(true, roots, null));
    }

    [HttpPost("categories/{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpsertCategory(string id, [FromBody] UpsertCatalogCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await catalogRepository.UpsertCategoryAsync(id, request, cancellationToken);
        return Ok(new ApiResponse<CatalogCategory>(true, category, null));
    }

    [HttpPatch("categories/{id}/sort")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateCategorySort(string id, [FromBody] UpdateCatalogCategorySortRequest request, CancellationToken cancellationToken)
    {
        var category = await catalogRepository.GetCategoryByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Category not found.")));
        }

        category.SortOrder = request.SortOrder;
        category.UpdatedAt = DateTimeOffset.UtcNow;
        var saved = await catalogRepository.SaveCategoryAsync(category, cancellationToken);
        return Ok(new ApiResponse<CatalogCategory>(true, saved, null));
    }

    [HttpPatch("categories/{id}/visibility")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateCategoryVisibility(string id, [FromBody] UpdateCatalogCategoryVisibilityRequest request, CancellationToken cancellationToken)
    {
        var category = await catalogRepository.GetCategoryByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Category not found.")));
        }

        category.IsVisible = request.IsVisible;
        category.UpdatedAt = DateTimeOffset.UtcNow;
        var saved = await catalogRepository.SaveCategoryAsync(category, cancellationToken);
        return Ok(new ApiResponse<CatalogCategory>(true, saved, null));
    }

    [HttpPatch("categories/{id}/enabled")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateCategoryEnabled(string id, [FromBody] UpdateCatalogCategoryEnabledRequest request, CancellationToken cancellationToken)
    {
        var category = await catalogRepository.GetCategoryByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Category not found.")));
        }

        category.IsEnabled = request.IsEnabled;
        category.UpdatedAt = DateTimeOffset.UtcNow;
        var saved = await catalogRepository.SaveCategoryAsync(category, cancellationToken);
        if (!request.IsEnabled)
        {
            await CascadeShelfOffByDisabledCategoryAsync(id, cancellationToken);
        }
        else
        {
            await CascadeMarkCategoryEnabledAsync(id, cancellationToken);
        }

        return Ok(new ApiResponse<CatalogCategory>(true, saved, null));
    }

    [HttpDelete("categories/{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteCategory(string id, CancellationToken cancellationToken)
    {
        var category = await catalogRepository.GetCategoryByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Category not found.")));
        }

        var children = await catalogRepository.GetChildCategoriesRecursiveAsync(id, cancellationToken);
        if (children.Count > 0)
        {
            return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Conflict, "Category has child categories.", new { childCount = children.Count })));
        }

        var items = await catalogRepository.GetItemsByCategoryIdAsync(id, cancellationToken);
        if (items.Count > 0)
        {
            return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Conflict, "Category has products.", new { itemCount = items.Count })));
        }

        await catalogRepository.DeleteCategoryAsync(id, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { deleted = id }, null));
    }

    private bool CanWriteShop(string? shopId)
    {
        if (User.IsInRole("admin"))
        {
            return true;
        }

        var effectiveShop = string.IsNullOrWhiteSpace(shopId) ? "shop-default" : shopId.Trim();
        var managedShops = ResolveManagedShopIds(User);
        return managedShops.Contains(effectiveShop, StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> ResolveManagedShopIds(ClaimsPrincipal user)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in user.Claims)
        {
            if (claim.Type is ClaimTypes.Role or "role" or "roles")
            {
                var value = claim.Value?.Trim();
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (value.StartsWith("shop:", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(value["shop:".Length..]);
                }
                else if (value.StartsWith("shop-manager:", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(value["shop-manager:".Length..]);
                }
            }

            if (claim.Type is "shop_id" or "shopId")
            {
                if (!string.IsNullOrWhiteSpace(claim.Value))
                {
                    result.Add(claim.Value.Trim());
                }
            }
        }

        return result;
    }

    private async Task CascadeShelfOffByDisabledCategoryAsync(string categoryId, CancellationToken cancellationToken)
    {
        var descendants = await catalogRepository.GetChildCategoriesRecursiveAsync(categoryId, cancellationToken);
        var targets = descendants.Select(x => x.Id).Append(categoryId).ToList();
        foreach (var targetId in targets)
        {
            var items = await catalogRepository.GetItemsByCategoryIdAsync(targetId, cancellationToken);
            foreach (var item in items)
            {
                item.CategoryEnabled = false;
                if (item.IsOnShelf)
                {
                    item.IsOnShelf = false;
                    item.AuditHistory.Insert(0, new CatalogAuditHistoryEntry
                    {
                        Action = "shelf_off_by_category_disable",
                        Note = $"category={targetId}",
                        OccurredAt = DateTimeOffset.UtcNow
                    });
                }

                item.UpdatedAt = DateTimeOffset.UtcNow;
                await catalogRepository.SaveAsync(item, cancellationToken);
            }
        }
    }

    private async Task CascadeMarkCategoryEnabledAsync(string categoryId, CancellationToken cancellationToken)
    {
        var descendants = await catalogRepository.GetChildCategoriesRecursiveAsync(categoryId, cancellationToken);
        var targets = descendants.Select(x => x.Id).Append(categoryId).ToList();
        foreach (var targetId in targets)
        {
            var items = await catalogRepository.GetItemsByCategoryIdAsync(targetId, cancellationToken);
            foreach (var item in items.Where(x => !x.CategoryEnabled))
            {
                item.CategoryEnabled = true;
                item.UpdatedAt = DateTimeOffset.UtcNow;
                item.AuditHistory.Insert(0, new CatalogAuditHistoryEntry
                {
                    Action = "category_enabled",
                    Note = $"category={targetId}",
                    OccurredAt = DateTimeOffset.UtcNow
                });
                await catalogRepository.SaveAsync(item, cancellationToken);
            }
        }
    }

    private static void SortCategoryNodes(List<CatalogCategoryTreeNode> nodes)
    {
        nodes.Sort((a, b) =>
        {
            var bySort = a.SortOrder.CompareTo(b.SortOrder);
            return bySort != 0 ? bySort : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        foreach (var node in nodes)
        {
            SortCategoryNodes(node.Children);
        }
    }
}
