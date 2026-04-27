using DaprFx.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public sealed class OrderFulfillmentController(
    IEventBus eventBus,
    IStateStore<Order> stateStore,
    ILogger<OrderFulfillmentController> logger) : ControllerBase
{
    private readonly IEventBus _eventBus = eventBus;
    private readonly IStateStore<Order> _stateStore = stateStore;
    private readonly ILogger<OrderFulfillmentController> _logger = logger;

    [HttpPost("{orderId}/sub-orders/{subOrderId}/ship")]
    public async Task<IActionResult> MarkSubOrderShipped(
        string orderId,
        string subOrderId,
        [FromBody] SubOrderShipRequest? request,
        CancellationToken cancellationToken)
    {
        var order = await _stateStore.GetAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Order not found.")));
        }

        if (order.Status != OrderStatus.Confirmed)
        {
            return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidFulfillmentState, "Order is not in a shippable state.", new { order.Status })));
        }

        var sub = order.SubOrders.FirstOrDefault(s => string.Equals(s.Id, subOrderId, StringComparison.Ordinal));
        if (sub is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.SubOrderNotFound, "Sub-order not found.")));
        }

        if (!OrderAccess.CanManageSubOrder(User, order, sub))
        {
            return OrderAccess.Forbidden();
        }

        SubOrderFulfillmentHelper.Normalize(sub);
        if (sub.FulfillmentStatus == SubOrderFulfillmentStatus.Shipped || sub.FulfillmentStatus == SubOrderFulfillmentStatus.Delivered)
        {
            return Ok(new ApiResponse<Order>(true, order, null));
        }

        if (sub.FulfillmentStatus == SubOrderFulfillmentStatus.Cancelled)
        {
            return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidFulfillmentState, "Sub-order was cancelled.", new { sub.FulfillmentStatus })));
        }

        if (sub.FulfillmentStatus != SubOrderFulfillmentStatus.PendingShipment)
        {
            return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidFulfillmentState, "Sub-order cannot be shipped from current state.", new { sub.FulfillmentStatus })));
        }

        sub.FulfillmentStatus = SubOrderFulfillmentStatus.Shipped;
        sub.ShippedAt = DateTimeOffset.UtcNow;
        sub.TrackingNumber = string.IsNullOrWhiteSpace(request?.TrackingNumber) ? null : request!.TrackingNumber.Trim();
        sub.CarrierCode = string.IsNullOrWhiteSpace(request?.CarrierCode) ? null : request!.CarrierCode.Trim();
        sub.CarrierName = string.IsNullOrWhiteSpace(request?.CarrierName) ? null : request!.CarrierName.Trim();
        sub.TrackingEvents.Add(new SubOrderTrackingEvent
        {
            Status = "Shipped",
            Message = "Sub-order shipped.",
            Source = "order-service",
            TrackingNumber = sub.TrackingNumber,
            CarrierCode = sub.CarrierCode,
            CarrierName = sub.CarrierName,
            CreatedAt = DateTimeOffset.UtcNow
        });
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _stateStore.SaveAsync(order.Id, order, cancellationToken);
        await _eventBus.PublishAsync(
            new OrderShippedEvent(order.Id, sub.Id, sub.ShopId, order.UserId, sub.TrackingNumber, sub.CarrierCode, sub.CarrierName, DateTimeOffset.UtcNow),
            topic: "order.shipped",
            cancellationToken: cancellationToken);
        _logger.LogInformation("SubOrder shipped. OrderId={OrderId}, SubOrderId={SubOrderId}", orderId, subOrderId);
        return Ok(new ApiResponse<Order>(true, order, null));
    }

    [HttpPost("{orderId}/sub-orders/{subOrderId}/deliver")]
    public async Task<IActionResult> MarkSubOrderDelivered(string orderId, string subOrderId, CancellationToken cancellationToken)
    {
        var order = await _stateStore.GetAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Order not found.")));
        }

        if (order.Status != OrderStatus.Confirmed && order.Status != OrderStatus.Completed)
        {
            return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidFulfillmentState, "Order is not in a deliverable state.", new { order.Status })));
        }

        var sub = order.SubOrders.FirstOrDefault(s => string.Equals(s.Id, subOrderId, StringComparison.Ordinal));
        if (sub is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.SubOrderNotFound, "Sub-order not found.")));
        }

        if (!OrderAccess.CanManageSubOrder(User, order, sub))
        {
            return OrderAccess.Forbidden();
        }

        SubOrderFulfillmentHelper.Normalize(sub);
        if (sub.FulfillmentStatus == SubOrderFulfillmentStatus.Delivered)
        {
            return Ok(new ApiResponse<Order>(true, order, null));
        }

        if (sub.FulfillmentStatus == SubOrderFulfillmentStatus.Cancelled)
        {
            return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidFulfillmentState, "Sub-order was cancelled.", new { sub.FulfillmentStatus })));
        }

        if (sub.FulfillmentStatus != SubOrderFulfillmentStatus.Shipped)
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidFulfillmentState, "Sub-order must be shipped before delivery.", new { sub.FulfillmentStatus })));
        }

        sub.FulfillmentStatus = SubOrderFulfillmentStatus.Delivered;
        sub.DeliveredAt = DateTimeOffset.UtcNow;
        sub.TrackingEvents.Add(new SubOrderTrackingEvent
        {
            Status = "Delivered",
            Message = "Sub-order delivered.",
            Source = "order-service",
            TrackingNumber = sub.TrackingNumber,
            CarrierCode = sub.CarrierCode,
            CarrierName = sub.CarrierName,
            CreatedAt = DateTimeOffset.UtcNow
        });
        order.UpdatedAt = DateTimeOffset.UtcNow;

        var completedNow = SubOrderFulfillmentHelper.AllActiveSubOrdersDelivered(order.SubOrders);
        if (completedNow)
        {
            order.Status = OrderStatus.Completed;
        }

        await _stateStore.SaveAsync(order.Id, order, cancellationToken);
        await _eventBus.PublishAsync(
            new OrderDeliveredEvent(order.Id, sub.Id, sub.ShopId, order.UserId, DateTimeOffset.UtcNow),
            topic: "order.delivered",
            cancellationToken: cancellationToken);
        if (completedNow)
        {
            await _eventBus.PublishAsync(
                new OrderCompletedEvent(order.Id, order.UserId, DateTimeOffset.UtcNow),
                topic: "order.completed",
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation("SubOrder delivered. OrderId={OrderId}, SubOrderId={SubOrderId}, OrderStatus={OrderStatus}", orderId, subOrderId, order.Status);
        return Ok(new ApiResponse<Order>(true, order, null));
    }
}
