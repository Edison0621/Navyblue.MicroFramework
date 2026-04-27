using DaprFx.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Abstractions;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrderPaymentController(
    IEventBus eventBus,
    IStateStore<Order> orderStore,
    IStateStore<PaymentPendingIndex> paymentPendingIndexStore,
    IStateStore<OrderPayIdempotencyRecord> payIdempotencyStore,
    IInventoryService inventoryService,
    ILogger<OrderPaymentController> logger) : ControllerBase
{
    private readonly IEventBus _eventBus = eventBus;
    private readonly IStateStore<Order> _orderStore = orderStore;
    private readonly IStateStore<PaymentPendingIndex> _paymentPendingIndexStore = paymentPendingIndexStore;
    private readonly IStateStore<OrderPayIdempotencyRecord> _payIdempotencyStore = payIdempotencyStore;
    private readonly IInventoryService _inventoryService = inventoryService;
    private readonly ILogger<OrderPaymentController> _logger = logger;

    [Authorize]
    [HttpPost("{orderId}/pay")]
    public async Task<IActionResult> SimulatePay(string orderId, [FromBody] SimulatePayRequest? request, CancellationToken cancellationToken)
    {
        var order = await _orderStore.GetAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Order not found.")));
        }

        if (!OrderAccess.CanAccessOrder(User, order))
        {
            return OrderAccess.Forbidden();
        }

        if (order.Status == OrderStatus.Confirmed)
        {
            return Ok(new ApiResponse<Order>(true, order, null));
        }

        if (order.Status != OrderStatus.AwaitingPayment)
        {
            return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.PaymentRequired, "Order is not awaiting payment.", new { order.Status })));
        }

        var idemKey = SanitizeIdempotencyKey(request?.IdempotencyKey);
        if (idemKey is not null)
        {
            var idemLookupKey = BuildPayIdempotencyStateKey(orderId, idemKey);
            var prior = await _payIdempotencyStore.GetAsync(idemLookupKey, cancellationToken);
            if (prior is not null)
            {
                var refreshed = await _orderStore.GetAsync(orderId, cancellationToken);
                return Ok(new ApiResponse<Order>(true, refreshed ?? order, null));
            }
        }

        var now = DateTimeOffset.UtcNow;
        order.Status = OrderStatus.Confirmed;
        order.PaidAt = now;
        order.UpdatedAt = now;
        await _orderStore.SaveAsync(order.Id, order, cancellationToken);
        await PaymentPendingIndexHelper.RemoveOrderIdAsync(_paymentPendingIndexStore, order.Id, cancellationToken);

        if (idemKey is not null)
        {
            var idemSaveKey = BuildPayIdempotencyStateKey(orderId, idemKey);
            await _payIdempotencyStore.SaveAsync(
                idemSaveKey,
                new OrderPayIdempotencyRecord { OrderId = order.Id, ProcessedAt = now },
                cancellationToken);
        }

        await _eventBus.PublishAsync(
            new OrderPaidEvent(order.Id, order.UserId, now),
            topic: "order.paid",
            cancellationToken: cancellationToken);
        _logger.LogInformation("Simulated payment captured. OrderId={OrderId}", orderId);
        return Ok(new ApiResponse<Order>(true, order, null));
    }

    [HttpPost("ops/expire-awaiting-payments")]
    public async Task<IActionResult> ExpireAwaitingPayments([FromQuery] int maxAgeMinutes = 30, CancellationToken cancellationToken = default)
    {
        var safeMinutes = Math.Clamp(maxAgeMinutes, 5, 24 * 60);
        var index = await _paymentPendingIndexStore.GetAsync(PaymentPendingIndex.StateKey, cancellationToken) ?? new PaymentPendingIndex();
        var snapshot = index.OrderIds.Distinct(StringComparer.Ordinal).ToList();
        var nextPending = new List<string>();
        var expiredCount = 0;

        foreach (var id in snapshot)
        {
            var order = await _orderStore.GetAsync(id, cancellationToken);
            if (order is null)
            {
                continue;
            }

            if (order.Status != OrderStatus.AwaitingPayment)
            {
                continue;
            }

            var deadline = order.PaymentDueAt ?? order.CreatedAt.AddMinutes(safeMinutes);
            if (DateTimeOffset.UtcNow <= deadline)
            {
                nextPending.Add(id);
                continue;
            }

            try
            {
                foreach (var line in order.SubOrders.SelectMany(s => s.Lines))
                {
                    await _inventoryService.ReleaseAsync(line.ProductId, new InventoryQuantityRequest(line.Quantity));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Inventory release failed while expiring order. OrderId={OrderId}", id);
                nextPending.Add(id);
                continue;
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var sub in order.SubOrders)
            {
                SubOrderFulfillmentHelper.Normalize(sub);
                sub.FulfillmentStatus = SubOrderFulfillmentStatus.Cancelled;
                sub.CancelledAt = now;
            }

            order.Status = OrderStatus.Cancelled;
            order.FailureReason = "Payment timeout.";
            order.UpdatedAt = now;
            await _orderStore.SaveAsync(order.Id, order, cancellationToken);
            await _eventBus.PublishAsync(
                new OrderCancelledEvent(order.Id, order.UserId, FullOrder: true, SubOrderId: null, now),
                topic: "order.cancelled",
                cancellationToken: cancellationToken);
            expiredCount++;
        }

        index.OrderIds = nextPending;
        await _paymentPendingIndexStore.SaveAsync(PaymentPendingIndex.StateKey, index, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { Expired = expiredCount, RemainingPending = nextPending.Count }, null));
    }

    private static string? SanitizeIdempotencyKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (trimmed.Length > 120)
        {
            trimmed = trimmed[..120];
        }

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string BuildPayIdempotencyStateKey(string orderId, string idempotencyKey)
        => $"order:pay-idem:{orderId}:{idempotencyKey}";
}
