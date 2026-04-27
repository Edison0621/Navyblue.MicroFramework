using DaprFx.Core;
using Microsoft.AspNetCore.Mvc;
using OrderService.Models;

namespace OrderService.Controllers;

[ApiController]
[Route("api/carts")]
public sealed class CartController(IStateStore<ShoppingCart> cartStateStore) : ControllerBase
{
    private readonly IStateStore<ShoppingCart> _cartStateStore = cartStateStore;

    internal static string BuildCartStateKey(string userId) => $"cart:{userId}";

    internal static List<CartLine> MergeLines(IReadOnlyList<CartLine> lines)
    {
        return lines
            .Where(x => !string.IsNullOrWhiteSpace(x.ProductId) && x.Quantity > 0)
            .GroupBy(x => x.ProductId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new CartLine { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetCart(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidUserId, "UserId is required.")));
        }

        var key = BuildCartStateKey(userId.Trim());
        var cart = await _cartStateStore.GetAsync(key, cancellationToken);
        if (cart is null)
        {
            cart = new ShoppingCart { UserId = userId.Trim(), Lines = [] };
        }

        cart.Lines = MergeLines(cart.Lines);
        return Ok(new ApiResponse<ShoppingCart>(true, cart, null));
    }

    [HttpPut("{userId}")]
    public async Task<IActionResult> ReplaceCart(string userId, [FromBody] ReplaceCartRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidUserId, "UserId is required.")));
        }

        var lines = (request.Lines ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.ProductId) && x.Quantity > 0)
            .Select(x => new CartLine { ProductId = x.ProductId.Trim(), Quantity = x.Quantity })
            .ToList();
        var merged = MergeLines(lines);
        var cart = new ShoppingCart
        {
            UserId = userId.Trim(),
            Lines = merged,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _cartStateStore.SaveAsync(BuildCartStateKey(userId.Trim()), cart, cancellationToken);
        return Ok(new ApiResponse<ShoppingCart>(true, cart, null));
    }
}
