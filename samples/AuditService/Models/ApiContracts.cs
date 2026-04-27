namespace AuditService.Models;

public sealed record ApiResponse<T>(bool Success, T? Data, ApiError? Error, string? TraceId = null);
public sealed record ApiError(string Code, string Message, object? Details = null);
public static class ApiErrorCodes
{
    public const string NotFound = "not_found";
    public const string InvalidRequest = "invalid_request";
    public const string Unauthorized = "unauthorized";
    public const string Conflict = "conflict";
    public const string UpstreamError = "upstream_error";
    public const string InternalError = "internal_error";
    public const string InvalidResponse = "invalid_response";
    public const string InvalidQuantity = "invalid_quantity";
    public const string InsufficientInventory = "insufficient_inventory";
    public const string InvalidPromotion = "invalid_promotion";
    public const string InventoryReservationFailed = "inventory_reservation_failed";
    public const string OrderCreationFailed = "order_creation_failed";
}
public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int Total);
public sealed record PageQuery(int Page = 1, int PageSize = 50)
{
    public int SafePage => Page <= 0 ? 1 : Page;
    public int SafePageSize => PageSize <= 0 ? 50 : Math.Min(PageSize, 200);
}
