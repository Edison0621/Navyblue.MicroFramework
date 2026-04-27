namespace NotificationService.Models;

public sealed record SendNotificationRequest(string Channel, string To, string Title, string Body);
public sealed record NotificationItem(string Id, string Channel, string To, string Title, string Body, DateTimeOffset CreatedAt);
public sealed record OrderCreatedEvent(
    string OrderId,
    string ProductId,
    int Quantity,
    DateTimeOffset CreatedAt,
    string? UserId = null,
    IReadOnlyList<OrderCreatedSubOrderPayload>? SubOrders = null);

public sealed record OrderCreatedSubOrderPayload(string ShopId, string SubOrderId, IReadOnlyList<OrderCreatedLinePayload> Lines);

public sealed record OrderCreatedLinePayload(string ProductId, string? SkuId, int Quantity, decimal UnitPrice);
public sealed record OrderCompensationFailedEvent(string OrderId, string ProductId, int Quantity, string Error, DateTimeOffset OccurredAt);
public sealed record OrderCancelledEvent(string OrderId, string? UserId, bool FullOrder, string? SubOrderId, DateTimeOffset OccurredAt);
public sealed record OrderCompletedEvent(string OrderId, string? UserId, DateTimeOffset OccurredAt);
public sealed record OrderPaidEvent(string OrderId, string? UserId, DateTimeOffset OccurredAt);
public sealed record AfterSaleRequestedEvent(
    string OrderId,
    string AfterSaleId,
    string UserId,
    string? SubOrderId,
    string Reason,
    decimal? RequestedAmount,
    DateTimeOffset RequestedAt);
public sealed record AfterSaleReviewedEvent(
    string OrderId,
    string AfterSaleId,
    string Status,
    string ReviewedByUserId,
    DateTimeOffset ReviewedAt,
    string? Note);
public sealed record OrderRefundedEvent(
    string OrderId,
    string AfterSaleId,
    string? UserId,
    decimal Amount,
    string RefundTransactionId,
    DateTimeOffset RefundedAt);
public sealed record OrderPaymentFailedEvent(
    string OrderId,
    string? UserId,
    string Status,
    string? TransactionId,
    string Reason,
    DateTimeOffset OccurredAt);
