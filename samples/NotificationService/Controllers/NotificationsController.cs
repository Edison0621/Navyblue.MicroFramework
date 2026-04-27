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
        var item = new NotificationItem(
            Guid.NewGuid().ToString("N"),
            "system",
            "ops",
            "Order Created",
            $"Order {payload.OrderId} created, product={payload.ProductId}, quantity={payload.Quantity}",
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
