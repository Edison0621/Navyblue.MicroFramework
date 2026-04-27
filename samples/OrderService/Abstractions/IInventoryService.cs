using DaprFx.Core;
using OrderService.Models;

namespace OrderService.Abstractions;

public interface IInventoryService
{
    [DaprInvoke("/api/inventory/{productId}/reserve")]
    Task<InventoryStockDto?> ReserveAsync(string productId, InventoryQuantityRequest request);

    [DaprInvoke("/api/inventory/{productId}/release")]
    Task<InventoryStockDto?> ReleaseAsync(string productId, InventoryQuantityRequest request);
}
