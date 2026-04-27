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
    public Guid? AddressId { get; set; }
    public string? ShipToReceiverName { get; set; }
    public string? ShipToPhone { get; set; }
    public string? ShipToRegion { get; set; }
    public string? ShipToDetail { get; set; }
}

public sealed class SubOrder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ShopId { get; set; } = string.Empty;
    public List<OrderLine> Lines { get; set; } = [];
    public decimal Subtotal { get; set; }
    public string FulfillmentStatus { get; set; } = SubOrderFulfillmentStatus.PendingShipment;
    public string? TrackingNumber { get; set; }
    public DateTimeOffset? ShippedAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}

public sealed class OrderLine
{
    public string ProductId { get; set; } = string.Empty;
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
