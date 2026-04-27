namespace OrderService.Models;

public sealed class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? UserId { get; set; }
    public List<SubOrder> SubOrders { get; set; } = [];
    public string? PromoCode { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public string Status { get; set; } = OrderStatus.Pending;
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PaymentDueAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public string? PaymentTransactionId { get; set; }
    public Guid? AddressId { get; set; }
    public string? ShipToReceiverName { get; set; }
    public string? ShipToPhone { get; set; }
    public string? ShipToRegion { get; set; }
    public string? ShipToDetail { get; set; }
    public List<AfterSaleRequest> AfterSales { get; set; } = [];
}

public sealed class SubOrder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ShopId { get; set; } = string.Empty;
    public List<OrderLine> Lines { get; set; } = [];
    public decimal Subtotal { get; set; }
    public string FulfillmentStatus { get; set; } = SubOrderFulfillmentStatus.PendingShipment;
    public string? TrackingNumber { get; set; }
    public string? CarrierCode { get; set; }
    public string? CarrierName { get; set; }
    public DateTimeOffset? ShippedAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public List<SubOrderTrackingEvent> TrackingEvents { get; set; } = [];
}

public sealed class OrderLine
{
    public string ProductId { get; set; } = string.Empty;
    public string? SkuId { get; set; }
    public string? SkuName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string ShopId { get; set; } = "shop-default";
}

public static class OrderStatus
{
    public const string Pending = "Pending";
    public const string AwaitingPayment = "AwaitingPayment";
    public const string Confirmed = "Confirmed";
    public const string Failed = "Failed";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public static class SubOrderFulfillmentStatus
{
    public const string PendingShipment = "PendingShipment";
    public const string Shipped = "Shipped";
    public const string Delivered = "Delivered";
    public const string Cancelled = "Cancelled";
}

public sealed class AfterSaleRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? SubOrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public decimal? RequestedAmount { get; set; }
    public string Status { get; set; } = AfterSaleStatus.Pending;
    public string RequestedByUserId { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? DecisionNote { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? RefundStatus { get; set; }
    public string? RefundTransactionId { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }
    public decimal? RefundedAmount { get; set; }
}

public static class AfterSaleStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public static class AfterSaleRefundStatus
{
    public const string NotRequired = "NotRequired";
    public const string Pending = "Pending";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}

public sealed class SubOrderTrackingEvent
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = "system";
    public string? TrackingNumber { get; set; }
    public string? CarrierCode { get; set; }
    public string? CarrierName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
