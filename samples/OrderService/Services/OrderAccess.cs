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

    public static bool CanManageSubOrder(ClaimsPrincipal user, Order order, SubOrder subOrder)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        var uid = GetUserId(user);
        if (!string.IsNullOrWhiteSpace(uid)
            && !string.IsNullOrWhiteSpace(order.UserId)
            && string.Equals(order.UserId, uid, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var shopId = subOrder.ShopId?.Trim();
        if (string.IsNullOrWhiteSpace(shopId))
        {
            return false;
        }

        var managedShopIds = ResolveManagedShopIds(user);
        return managedShopIds.Contains(shopId, StringComparer.OrdinalIgnoreCase);
    }

    public static bool CanAccessShop(ClaimsPrincipal user, string shopId)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        var normalized = shopId?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var managedShopIds = ResolveManagedShopIds(user);
        return managedShopIds.Contains(normalized);
    }

    private static HashSet<string> ResolveManagedShopIds(ClaimsPrincipal user)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roleValues = user.FindAll(ClaimTypes.Role).Select(x => x.Value);
        foreach (var role in roleValues)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                continue;
            }

            var trimmed = role.Trim();
            const string shopRolePrefix = "shop:";
            if (trimmed.StartsWith(shopRolePrefix, StringComparison.OrdinalIgnoreCase) && trimmed.Length > shopRolePrefix.Length)
            {
                set.Add(trimmed[shopRolePrefix.Length..]);
                continue;
            }

            const string managerPrefix = "shop-manager:";
            if (trimmed.StartsWith(managerPrefix, StringComparison.OrdinalIgnoreCase) && trimmed.Length > managerPrefix.Length)
            {
                set.Add(trimmed[managerPrefix.Length..]);
            }
        }

        var directClaimValues = user.FindAll("shop_id").Select(x => x.Value);
        foreach (var shopId in directClaimValues)
        {
            if (!string.IsNullOrWhiteSpace(shopId))
            {
                set.Add(shopId.Trim());
            }
        }

        return set;
    }

    public static IActionResult Forbidden() =>
        new ObjectResult(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "You cannot access this order.")))
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
}
