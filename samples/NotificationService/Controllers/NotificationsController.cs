using Dapr;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Models;
using NotificationService.Repositories;

namespace NotificationService.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(INotificationRepository notificationRepository) : ControllerBase
{
    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendNotificationRequest request, CancellationToken cancellationToken)
    {
        var item = new NotificationItem(
            Guid.NewGuid().ToString("N"),
            request.Channel,
            request.To,
            request.Title,
            request.Body,
            DateTimeOffset.UtcNow);
        await notificationRepository.AppendAsync(item, cancellationToken);
        return Accepted($"/api/notifications/{item.Id}", new ApiResponse<NotificationItem>(true, item, null));
    }

    [HttpPost("subscriptions/order-created")]
    [Topic("orderpubsub", "order.created")]
    public async Task<IActionResult> OnOrderCreated([FromBody] OrderCreatedEvent payload, CancellationToken cancellationToken)
    {
        var subSummary = payload.SubOrders is { Count: > 0 } subs
            ? string.Join(", ", subs.Select(s => $"{s.ShopId}({s.Lines.Count} lines)"))
            : null;
        var body = subSummary is null
            ? $"Order {payload.OrderId} created, product={payload.ProductId}, quantity={payload.Quantity}"
            : $"Order {payload.OrderId} created, user={payload.UserId ?? "-"}, shops=[{subSummary}], primaryProduct={payload.ProductId}, totalQty={payload.Quantity}";
        var item = new NotificationItem(
            Guid.NewGuid().ToString("N"),
            "system",
            "ops",
            "Order Created",
            body,
            DateTimeOffset.UtcNow);
        await notificationRepository.AppendAsync(item, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "consumed" }, null));
    }

    [HttpPost("subscriptions/order-compensation-failed")]
    [Topic("orderpubsub", "order.compensation.failed")]
    public async Task<IActionResult> OnOrderCompensationFailed([FromBody] OrderCompensationFailedEvent payload, CancellationToken cancellationToken)
    {
        var item = new NotificationItem(
            Guid.NewGuid().ToString("N"),
            "alert",
            "ops",
            "Order Compensation Failed",
            $"Order {payload.OrderId} compensation failed: {payload.Error}",
            DateTimeOffset.UtcNow);
        await notificationRepository.AppendAsync(item, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "consumed" }, null));
    }

    [HttpPost("subscriptions/order-cancelled")]
    [Topic("orderpubsub", "order.cancelled")]
    public async Task<IActionResult> OnOrderCancelled([FromBody] OrderCancelledEvent payload, CancellationToken cancellationToken)
    {
        var scope = payload.FullOrder ? "full" : "sub_order";
        var item = new NotificationItem(
            Guid.NewGuid().ToString("N"),
            "system",
            "ops",
            "Order Cancelled",
            $"Order {payload.OrderId} cancelled ({scope}), user={payload.UserId ?? "-"}, subOrderId={payload.SubOrderId ?? "-"}",
            DateTimeOffset.UtcNow);
        await notificationRepository.AppendAsync(item, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "consumed" }, null));
    }

    [HttpPost("subscriptions/order-paid")]
    [Topic("orderpubsub", "order.paid")]
    public async Task<IActionResult> OnOrderPaid([FromBody] OrderPaidEvent payload, CancellationToken cancellationToken)
    {
        var item = new NotificationItem(
            Guid.NewGuid().ToString("N"),
            "system",
            "ops",
            "Order Paid",
            $"Order {payload.OrderId} paid (simulated), user={payload.UserId ?? "-"}",
            DateTimeOffset.UtcNow);
        await notificationRepository.AppendAsync(item, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "consumed" }, null));
    }

    [HttpPost("subscriptions/order-completed")]
    [Topic("orderpubsub", "order.completed")]
    public async Task<IActionResult> OnOrderCompleted([FromBody] OrderCompletedEvent payload, CancellationToken cancellationToken)
    {
        var item = new NotificationItem(
            Guid.NewGuid().ToString("N"),
            "system",
            "ops",
            "Order Completed",
            $"Order {payload.OrderId} completed, user={payload.UserId ?? "-"}",
            DateTimeOffset.UtcNow);
        await notificationRepository.AppendAsync(item, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "consumed" }, null));
    }

    [HttpPost("subscriptions/order-aftersale-requested")]
    [Topic("orderpubsub", "order.aftersale.requested")]
    public async Task<IActionResult> OnAfterSaleRequested([FromBody] AfterSaleRequestedEvent payload, CancellationToken cancellationToken)
    {
        var item = new NotificationItem(
            Guid.NewGuid().ToString("N"),
            "system",
            "ops",
            "After-sale Requested",
            $"Order {payload.OrderId} after-sale requested, afterSaleId={payload.AfterSaleId}, user={payload.UserId}, subOrderId={payload.SubOrderId ?? "-"}, reason={payload.Reason}",
            DateTimeOffset.UtcNow);
        await notificationRepository.AppendAsync(item, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "consumed" }, null));
    }

    [HttpPost("subscriptions/order-aftersale-reviewed")]
    [Topic("orderpubsub", "order.aftersale.reviewed")]
    public async Task<IActionResult> OnAfterSaleReviewed([FromBody] AfterSaleReviewedEvent payload, CancellationToken cancellationToken)
    {
        var item = new NotificationItem(
            Guid.NewGuid().ToString("N"),
            "system",
            "ops",
            "After-sale Reviewed",
            $"Order {payload.OrderId} after-sale reviewed, afterSaleId={payload.AfterSaleId}, status={payload.Status}, reviewer={payload.ReviewedByUserId}",
            DateTimeOffset.UtcNow);
        await notificationRepository.AppendAsync(item, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "consumed" }, null));
    }

    [HttpPost("subscriptions/order-refunded")]
    [Topic("orderpubsub", "order.refunded")]
    public async Task<IActionResult> OnOrderRefunded([FromBody] OrderRefundedEvent payload, CancellationToken cancellationToken)
    {
        var item = new NotificationItem(
            Guid.NewGuid().ToString("N"),
            "system",
            "ops",
            "Order Refunded",
            $"Order {payload.OrderId} refunded amount={payload.Amount}, afterSaleId={payload.AfterSaleId}, refundTx={payload.RefundTransactionId}",
            DateTimeOffset.UtcNow);
        await notificationRepository.AppendAsync(item, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "consumed" }, null));
    }

    [HttpPost("subscriptions/order-payment-failed")]
    [Topic("orderpubsub", "order.payment.failed")]
    public async Task<IActionResult> OnOrderPaymentFailed([FromBody] OrderPaymentFailedEvent payload, CancellationToken cancellationToken)
    {
        var item = new NotificationItem(
            Guid.NewGuid().ToString("N"),
            "alert",
            "ops",
            "Order Payment Failed",
            $"Order {payload.OrderId} payment failed status={payload.Status}, tx={payload.TransactionId ?? "-"}, reason={payload.Reason}",
            DateTimeOffset.UtcNow);
        await notificationRepository.AppendAsync(item, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "consumed" }, null));
    }

    [HttpPost("subscriptions/order-shipped")]
    [Topic("orderpubsub", "order.shipped")]
    public async Task<IActionResult> OnOrderShipped([FromBody] OrderShippedEvent payload, CancellationToken cancellationToken)
    {
        var item = new NotificationItem(
            Guid.NewGuid().ToString("N"),
            "system",
            "ops",
            "Order Shipped",
            $"Order {payload.OrderId} subOrder={payload.SubOrderId} shipped, shop={payload.ShopId}, tracking={payload.TrackingNumber ?? "-"}, carrier={payload.CarrierCode ?? "-"}",
            DateTimeOffset.UtcNow);
        await notificationRepository.AppendAsync(item, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "consumed" }, null));
    }

    [HttpPost("subscriptions/order-delivered")]
    [Topic("orderpubsub", "order.delivered")]
    public async Task<IActionResult> OnOrderDelivered([FromBody] OrderDeliveredEvent payload, CancellationToken cancellationToken)
    {
        var item = new NotificationItem(
            Guid.NewGuid().ToString("N"),
            "system",
            "ops",
            "Order Delivered",
            $"Order {payload.OrderId} subOrder={payload.SubOrderId} delivered, shop={payload.ShopId}",
            DateTimeOffset.UtcNow);
        await notificationRepository.AppendAsync(item, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "consumed" }, null));
    }

    [HttpGet]
    public async Task<IActionResult> GetRecent([FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var query = new PageQuery(page, pageSize);
        var items = await notificationRepository.GetRecentAsync(query.SafePage * query.SafePageSize, cancellationToken);
        var total = items.Count;
        var pagedItems = items.Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize).ToList();
        var data = new PagedResult<NotificationItem>(pagedItems, query.SafePage, query.SafePageSize, total);
        return Ok(new ApiResponse<PagedResult<NotificationItem>>(true, data, null));
    }
}
