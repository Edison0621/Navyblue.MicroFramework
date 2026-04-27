using Microsoft.AspNetCore.Mvc;
using OrderService.Abstractions;
using OrderService.Models;

namespace OrderService.Services;

public static class UserStatusGuard
{
    public static async Task<IActionResult?> EnsureUserIsActiveAsync(
        ControllerBase controller,
        IUserService userService,
        string userId,
        CancellationToken cancellationToken)
    {
        ApiResponse<UserAuthProfileDto>? resp;
        try
        {
            resp = await userService.GetUserInternalAsync(userId);
        }
        catch
        {
            return controller.StatusCode(
                StatusCodes.Status502BadGateway,
                new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.UpstreamError, "Failed to verify user status.")));
        }

        if (resp is not { Success: true, Data: not null })
        {
            return controller.StatusCode(
                StatusCodes.Status502BadGateway,
                new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.UpstreamError, "Failed to verify user status.")));
        }

        if (!string.Equals(resp.Data.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            return controller.StatusCode(
                StatusCodes.Status403Forbidden,
                new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.UserDisabled, "User account is not active.", new { resp.Data.Status })));
        }

        return null;
    }
}
