using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrderService.Models;

namespace OrderService.Services;

internal static class OrderAccess
{
    public static string? GetUserId(ClaimsPrincipal user) =>
        string.IsNullOrWhiteSpace(user.FindFirstValue(ClaimTypes.NameIdentifier))
            ? null
            : user.FindFirstValue(ClaimTypes.NameIdentifier)!.Trim();

    public static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole("admin");

    public static bool CanAccessOrder(ClaimsPrincipal user, Order order)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        var uid = GetUserId(user);
        if (uid is null || string.IsNullOrWhiteSpace(order.UserId))
        {
            return false;
        }

        return string.Equals(order.UserId, uid, StringComparison.OrdinalIgnoreCase);
    }

    public static IActionResult Forbidden() =>
        new ObjectResult(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "You cannot access this order.")))
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
}
