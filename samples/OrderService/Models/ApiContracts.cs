namespace OrderService.Models;

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
    public const string EmptyCart = "empty_cart";
    public const string InvalidUserId = "invalid_user_id";
    public const string SubOrderNotFound = "sub_order_not_found";
    public const string InvalidFulfillmentState = "invalid_fulfillment_state";
    public const string CannotCancel = "cannot_cancel";
    public const string PaymentRequired = "payment_required";
    public const string InvalidAddress = "invalid_address";
    public const string InvalidAfterSale = "invalid_after_sale";
    public const string InvalidAfterSaleState = "invalid_after_sale_state";
    public const string InvalidSignature = "invalid_signature";
    public const string UserDisabled = "user_disabled";
}
public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int Total);
public sealed record PageQuery(int Page = 1, int PageSize = 50)
{
    public int SafePage => Page <= 0 ? 1 : Page;
    public int SafePageSize => PageSize <= 0 ? 50 : Math.Min(PageSize, 200);
}
