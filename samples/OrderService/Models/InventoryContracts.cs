namespace OrderService.Models;

public sealed record InventoryQuantityRequest(int Quantity);

public sealed record InventoryStockDto(string ProductId, int Quantity, DateTimeOffset UpdatedAt);
