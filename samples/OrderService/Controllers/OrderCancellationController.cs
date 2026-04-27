using DaprFx.Core;
using Microsoft.AspNetCore.Mvc;
using OrderService.Abstractions;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrderCancellationController(
    IEventBus eventBus,
    IStateStore<Order> stateStore,
    IStateStore<PaymentPendingIndex> paymentPendingIndexStore,
    IInventoryService inventoryService,
    ILogger<OrderCancellationController> logger) : ControllerBase
{
    private readonly IEventBus _eventBus = eventBus;
    private readonly IStateStore<Order> _stateStore = stateStore;
    private readonly IStateStore<PaymentPendingIndex> _paymentPendingIndexStore = paymentPendingIndexStore;
    private readonly IInventoryService _inventoryService = inventoryService;
    private readonly ILogger<OrderCancellationController> _logger = logger;

    [HttpPost("{orderId}/cancel")]
    public async Task<IActionResult> CancelWholeOrder(string orderId, CancellationToken cancellationToken)
    {
        var order = await _stateStore.GetAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Order not found.")));
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            return Ok(new ApiResponse<Order>(true, order, null));
        }

        if (order.Status != OrderStatus.Confirmed && order.Status != OrderStatus.AwaitingPayment)
        {
            return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.CannotCancel, "Only orders awaiting payment or confirmed can be fully cancelled.", new { order.Status })));
        }

        foreach (var sub in order.SubOrders)
        {
            SubOrderFulfillmentHelper.Normalize(sub);
            if (sub.FulfillmentStatus != SubOrderFulfillmentStatus.PendingShipment)
            {
                return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.CannotCancel, "Cannot cancel: a sub-order is no longer pending shipment.", new { sub.Id, sub.FulfillmentStatus })));
            }
        }

        try
        {
            await ReleaseLinesAsync(order.SubOrders.SelectMany(s => s.Lines), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inventory release failed during full cancel. OrderId={OrderId}", orderId);
            return StatusCode(StatusCodes.Status502BadGateway, new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.UpstreamError, "Inventory release failed.", new { error = ex.Message })));
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var sub in order.SubOrders)
        {
            sub.FulfillmentStatus = SubOrderFulfillmentStatus.Cancelled;
            sub.CancelledAt = now;
        }

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = now;
        await _stateStore.SaveAsync(order.Id, order, cancellationToken);
        await PaymentPendingIndexHelper.RemoveOrderIdAsync(_paymentPendingIndexStore, order.Id, cancellationToken);
        await _eventBus.PublishAsync(
            new OrderCancelledEvent(order.Id, order.UserId, FullOrder: true, SubOrderId: null, now),
            topic: "order.cancelled",
            cancellationToken: cancellationToken);
        _logger.LogInformation("Order cancelled. OrderId={OrderId}", orderId);
        return Ok(new ApiResponse<Order>(true, order, null));
    }

    [HttpPost("{orderId}/sub-orders/{subOrderId}/cancel")]
    public async Task<IActionResult> CancelSubOrder(string orderId, string subOrderId, CancellationToken cancellationToken)
    {
        var order = await _stateStore.GetAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Order not found.")));
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            return Ok(new ApiResponse<Order>(true, order, null));
        }

        if (order.Status != OrderStatus.Confirmed && order.Status != OrderStatus.AwaitingPayment)
        {
            return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.CannotCancel, "Sub-order cannot be cancelled in current order state.", new { order.Status })));
        }

        var sub = order.SubOrders.FirstOrDefault(s => string.Equals(s.Id, subOrderId, StringComparison.Ordinal));
        if (sub is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.SubOrderNotFound, "Sub-order not found.")));
        }

        SubOrderFulfillmentHelper.Normalize(sub);
        if (sub.FulfillmentStatus == SubOrderFulfillmentStatus.Cancelled)
        {
            return Ok(new ApiResponse<Order>(true, order, null));
        }

        if (sub.FulfillmentStatus != SubOrderFulfillmentStatus.PendingShipment)
        {
            return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.CannotCancel, "Only pending sub-orders can be cancelled.", new { sub.FulfillmentStatus })));
        }

        try
        {
            await ReleaseLinesAsync(sub.Lines, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inventory release failed during sub-order cancel. OrderId={OrderId}, SubOrderId={SubOrderId}", orderId, subOrderId);
            return StatusCode(StatusCodes.Status502BadGateway, new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.UpstreamError, "Inventory release failed.", new { error = ex.Message })));
        }

        var now = DateTimeOffset.UtcNow;
        sub.FulfillmentStatus = SubOrderFulfillmentStatus.Cancelled;
        sub.CancelledAt = now;
        order.UpdatedAt = now;

        var fullOrderNow = order.SubOrders.All(s =>
        {
            SubOrderFulfillmentHelper.Normalize(s);
            return s.FulfillmentStatus == SubOrderFulfillmentStatus.Cancelled;
        });
        if (fullOrderNow)
        {
            order.Status = OrderStatus.Cancelled;
        }

        await _stateStore.SaveAsync(order.Id, order, cancellationToken);
        if (fullOrderNow)
        {
            await PaymentPendingIndexHelper.RemoveOrderIdAsync(_paymentPendingIndexStore, order.Id, cancellationToken);
        }

        await _eventBus.PublishAsync(
            new OrderCancelledEvent(order.Id, order.UserId, fullOrderNow, subOrderId, now),
            topic: "order.cancelled",
            cancellationToken: cancellationToken);
        _logger.LogInformation("SubOrder cancelled. OrderId={OrderId}, SubOrderId={SubOrderId}, OrderStatus={OrderStatus}", orderId, subOrderId, order.Status);
        return Ok(new ApiResponse<Order>(true, order, null));
    }

    private async Task ReleaseLinesAsync(IEnumerable<OrderLine> lines, CancellationToken cancellationToken)
    {
        foreach (var line in lines)
        {
            await _inventoryService.ReleaseAsync(line.ProductId, new InventoryQuantityRequest(line.Quantity));
        }
    }
}
