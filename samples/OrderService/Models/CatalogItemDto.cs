namespace OrderService.Models;

public sealed class CatalogItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public bool IsOnShelf { get; set; }
    public string? AuditStatus { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? ShopId { get; set; }
    public string? CategoryId { get; set; }
    public bool CategoryEnabled { get; set; } = true;
    public List<CatalogSkuDto> Skus { get; set; } = [];
}

public sealed class CatalogSkuDto
{
    public string SkuId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
}
