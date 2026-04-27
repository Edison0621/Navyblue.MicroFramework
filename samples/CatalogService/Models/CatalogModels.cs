namespace CatalogService.Models;

public sealed record UpsertCatalogItemRequest(
    string Name,
    decimal Price,
    bool IsActive,
    string? ShopId = null,
    string? CategoryId = null,
    IReadOnlyList<UpsertCatalogSkuRequest>? Skus = null);

public sealed record UpsertCatalogSkuRequest(
    string SkuId,
    string Name,
    decimal? Price = null,
    bool IsActive = true);

public sealed record SubmitCatalogItemForReviewRequest(string? Note = null);
public sealed record ApproveCatalogItemRequest(string? Note = null);
public sealed record RejectCatalogItemRequest(string Reason, string? Note = null);
public sealed record SetCatalogItemShelfRequest(bool IsOnShelf);
public sealed record SetCatalogItemShelfScheduleRequest(DateTimeOffset? OnAt, DateTimeOffset? OffAt);

public sealed record UpsertCatalogCategoryRequest(
    string Name,
    string? ParentId = null,
    int SortOrder = 0,
    bool IsVisible = true,
    bool IsEnabled = true);

public sealed record UpdateCatalogCategorySortRequest(int SortOrder);
public sealed record UpdateCatalogCategoryVisibilityRequest(bool IsVisible);
public sealed record UpdateCatalogCategoryEnabledRequest(bool IsEnabled);

public sealed class CatalogItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string ShopId { get; set; } = "shop-default";
    public string? CategoryId { get; set; }
    public bool CategoryEnabled { get; set; } = true;
    public List<CatalogSku> Skus { get; set; } = [];
    public string? AuditStatus { get; set; }
    public string? AuditReason { get; set; }
    public bool IsOnShelf { get; set; }
    public DateTimeOffset? ScheduledOnAt { get; set; }
    public DateTimeOffset? ScheduledOffAt { get; set; }
    public DateTimeOffset? LastSubmittedAt { get; set; }
    public DateTimeOffset? LastApprovedAt { get; set; }
    public DateTimeOffset? LastRejectedAt { get; set; }
    public List<CatalogAuditHistoryEntry> AuditHistory { get; set; } = [];
}

public sealed record CatalogSku(
    string SkuId,
    string Name,
    decimal Price,
    bool IsActive);

public sealed class CatalogCategory
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CatalogCategoryTreeNode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; }
    public bool IsEnabled { get; set; }
    public List<CatalogCategoryTreeNode> Children { get; set; } = [];
}

public sealed class CatalogAuditHistoryEntry
{
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class CatalogAuditStatus
{
    public const string Draft = "Draft";
    public const string PendingReview = "PendingReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}
