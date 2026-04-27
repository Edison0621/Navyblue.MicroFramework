using InventoryService.Models;
using InventoryService.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/inventory")]
public sealed class InventoryController(IInventoryRepository inventoryRepository) : ControllerBase
{
    private const int DefaultReservationTtlMinutes = 30;

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
        var now = DateTimeOffset.UtcNow;
        var ttlMinutes = Math.Clamp(request.TtlMinutes ?? DefaultReservationTtlMinutes, 1, 24 * 60);
        var reservationId = string.IsNullOrWhiteSpace(request.ReservationId) ? Guid.NewGuid().ToString("N") : request.ReservationId.Trim();
        var bucket = await inventoryRepository.GetReservationBucketAsync(productId, cancellationToken);
        bucket.Entries.Add(new InventoryReservationEntry
        {
            ReservationId = reservationId,
            Quantity = request.Quantity,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(ttlMinutes),
            Released = false
        });
        if (bucket.Entries.Count > 5000)
        {
            bucket.Entries = bucket.Entries.TakeLast(5000).ToList();
        }

        await inventoryRepository.SaveReservationBucketAsync(productId, bucket, cancellationToken);
        await EnsureProductIndexedAsync(productId, cancellationToken);
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
        var bucket = await inventoryRepository.GetReservationBucketAsync(productId, cancellationToken);
        MarkReleasedReservations(bucket, request.ReservationId, request.Quantity);
        await inventoryRepository.SaveReservationBucketAsync(productId, bucket, cancellationToken);
        await EnsureProductIndexedAsync(productId, cancellationToken);
        return Ok(new ApiResponse<InventoryStock>(true, updated, null));
    }

    [HttpPost("ops/reclaim-expired-reservations")]
    public async Task<IActionResult> ReclaimExpiredReservations([FromQuery] int take = 200, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var safeTake = Math.Clamp(take, 1, 2000);
        var ledger = await inventoryRepository.GetReservationLedgerAsync(cancellationToken);
        var productIds = ledger.ProductIds.Take(safeTake).ToList();

        var reclaimedReservations = 0;
        var reclaimedQuantity = 0;
        var impactedProducts = 0;
        foreach (var productId in productIds)
        {
            var bucket = await inventoryRepository.GetReservationBucketAsync(productId, cancellationToken);
            var expired = bucket.Entries
                .Where(x => !x.Released && x.ExpiresAt <= now)
                .ToList();
            if (expired.Count == 0)
            {
                continue;
            }

            var quantity = expired.Sum(x => x.Quantity);
            if (quantity > 0)
            {
                var stock = await inventoryRepository.GetAsync(productId, cancellationToken)
                    ?? new InventoryStock(productId, 0, DateTimeOffset.UtcNow);
                await inventoryRepository.SetAsync(productId, stock.Quantity + quantity, cancellationToken);
                reclaimedQuantity += quantity;
            }

            foreach (var entry in expired)
            {
                entry.Released = true;
                entry.ReleaseReason = "expired";
                entry.ReleasedAt = now;
                reclaimedReservations++;
            }

            impactedProducts++;
            await inventoryRepository.SaveReservationBucketAsync(productId, bucket, cancellationToken);
        }

        return Ok(new ApiResponse<object>(true, new
        {
            checkedProducts = productIds.Count,
            impactedProducts,
            reclaimedReservations,
            reclaimedQuantity
        }, null));
    }

    private async Task EnsureProductIndexedAsync(string productId, CancellationToken cancellationToken)
    {
        var ledger = await inventoryRepository.GetReservationLedgerAsync(cancellationToken);
        if (ledger.ProductIds.Any(x => string.Equals(x, productId, StringComparison.Ordinal)))
        {
            return;
        }

        ledger.ProductIds.Add(productId);
        await inventoryRepository.SaveReservationLedgerAsync(ledger, cancellationToken);
    }

    private static void MarkReleasedReservations(InventoryReservationBucket bucket, string? reservationId, int quantity)
    {
        if (quantity <= 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var remaining = quantity;
        if (!string.IsNullOrWhiteSpace(reservationId))
        {
            var exact = bucket.Entries
                .Where(x => !x.Released && string.Equals(x.ReservationId, reservationId, StringComparison.Ordinal))
                .OrderBy(x => x.CreatedAt)
                .ToList();
            foreach (var item in exact)
            {
                if (remaining <= 0)
                {
                    break;
                }

                item.Released = true;
                item.ReleaseReason = "manual";
                item.ReleasedAt = now;
                remaining -= item.Quantity;
            }
        }

        if (remaining <= 0)
        {
            return;
        }

        var candidates = bucket.Entries
            .Where(x => !x.Released)
            .OrderBy(x => x.ExpiresAt)
            .ThenBy(x => x.CreatedAt)
            .ToList();
        foreach (var item in candidates)
        {
            if (remaining <= 0)
            {
                break;
            }

            item.Released = true;
            item.ReleaseReason = "manual";
            item.ReleasedAt = now;
            remaining -= item.Quantity;
        }
    }
}
