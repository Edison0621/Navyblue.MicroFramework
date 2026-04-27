using DaprFx.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Abstractions;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public sealed class OrderTrackingController(
    IStateStore<Order> orderStore,
    ShipmentTrackingOptions shipmentTrackingOptions,
    IShipmentTrackingProvider shipmentTrackingProvider) : ControllerBase
{
    [HttpGet("{orderId}/sub-orders/{subOrderId}/tracking")]
    public async Task<IActionResult> GetSubOrderTracking(string orderId, string subOrderId, CancellationToken cancellationToken)
    {
        var order = await orderStore.GetAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Order not found.")));
        }

        var sub = order.SubOrders.FirstOrDefault(x => string.Equals(x.Id, subOrderId, StringComparison.Ordinal));
        if (sub is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.SubOrderNotFound, "Sub-order not found.")));
        }

        if (!OrderAccess.CanManageSubOrder(User, order, sub))
        {
            return OrderAccess.Forbidden();
        }

        var events = await shipmentTrackingProvider.QueryAsync(
            sub.CarrierCode,
            sub.TrackingNumber,
            sub.TrackingEvents,
            cancellationToken);
        var ordered = events
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
        return Ok(new ApiResponse<object>(true, new
        {
            provider = shipmentTrackingOptions.Provider,
            source = shipmentTrackingOptions.SourceName,
            events = ordered
        }, null));
    }
}
