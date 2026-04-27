namespace InventoryService.Models;

public sealed record SetStockRequest(int Quantity);
public sealed record ReserveStockRequest(int Quantity);
public sealed record ReleaseStockRequest(int Quantity);
public sealed record InventoryStock(string ProductId, int Quantity, DateTimeOffset UpdatedAt);
