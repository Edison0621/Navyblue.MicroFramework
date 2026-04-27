using InventoryService.Models;
using InventoryService.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/inventory")]
public sealed class InventoryController(IInventoryRepository inventoryRepository) : ControllerBase
{
    [HttpGet("{productId}")]
    public async Task<IActionResult> GetStock(string productId, CancellationToken cancellationToken)
    {
        var stock = await inventoryRepository.GetAsync(productId, cancellationToken);
        return stock is null
            ? NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Inventory stock not found.")))
            : Ok(new ApiResponse<InventoryStock>(true, stock, null));
    }

    [HttpPut("{productId}")]
    public async Task<IActionResult> SetStock(string productId, [FromBody] SetStockRequest request, CancellationToken cancellationToken)
    {
        var stock = await inventoryRepository.SetAsync(productId, request.Quantity, cancellationToken);
        return Ok(new ApiResponse<InventoryStock>(true, stock, null));
    }

    [HttpPost("{productId}/reserve")]
    public async Task<IActionResult> ReserveStock(string productId, [FromBody] ReserveStockRequest request, CancellationToken cancellationToken)
    {
        var current = await inventoryRepository.GetAsync(productId, cancellationToken)
            ?? new InventoryStock(productId, 0, DateTimeOffset.UtcNow);
        if (request.Quantity <= 0)
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidQuantity, "Quantity must be greater than zero.")));
        }

        if (current.Quantity < request.Quantity)
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InsufficientInventory, "Insufficient inventory.", new { available = current.Quantity })));
        }

        var updated = await inventoryRepository.SetAsync(productId, current.Quantity - request.Quantity, cancellationToken);
        return Ok(new ApiResponse<InventoryStock>(true, updated, null));
    }

    [HttpPost("{productId}/release")]
    public async Task<IActionResult> ReleaseStock(string productId, [FromBody] ReleaseStockRequest request, CancellationToken cancellationToken)
    {
        var current = await inventoryRepository.GetAsync(productId, cancellationToken)
            ?? new InventoryStock(productId, 0, DateTimeOffset.UtcNow);
        if (request.Quantity <= 0)
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidQuantity, "Quantity must be greater than zero.")));
        }

        var updated = await inventoryRepository.SetAsync(productId, current.Quantity + request.Quantity, cancellationToken);
        return Ok(new ApiResponse<InventoryStock>(true, updated, null));
    }
}
