using DaprFx.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("api/carts")]
[Authorize]
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

    [HttpGet("me")]
    public async Task<IActionResult> GetMyCart(CancellationToken cancellationToken)
    {
        var userId = OrderAccess.GetUserId(User);
        if (userId is null)
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Missing user identity.")));
        }

        var key = BuildCartStateKey(userId);
        var cart = await _cartStateStore.GetAsync(key, cancellationToken);
        if (cart is null)
        {
            cart = new ShoppingCart { UserId = userId, Lines = [] };
        }

        cart.Lines = MergeLines(cart.Lines);
        return Ok(new ApiResponse<ShoppingCart>(true, cart, null));
    }

    [HttpPut("me")]
    public async Task<IActionResult> ReplaceMyCart([FromBody] ReplaceCartRequest request, CancellationToken cancellationToken)
    {
        var userId = OrderAccess.GetUserId(User);
        if (userId is null)
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Missing user identity.")));
        }

        var lines = (request.Lines ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.ProductId) && x.Quantity > 0)
            .Select(x => new CartLine { ProductId = x.ProductId.Trim(), Quantity = x.Quantity })
            .ToList();
        var merged = MergeLines(lines);
        var cart = new ShoppingCart
        {
            UserId = userId,
            Lines = merged,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _cartStateStore.SaveAsync(BuildCartStateKey(userId), cart, cancellationToken);
        return Ok(new ApiResponse<ShoppingCart>(true, cart, null));
    }
}
