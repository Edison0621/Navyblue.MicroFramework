using DaprFx.Core;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
    PaymentCallbackOptions paymentCallbackOptions,
    IUserService userService,
    IPaymentGateway paymentGateway,
    IInventoryService inventoryService,
    ILogger<OrderPaymentController> logger) : ControllerBase
{
    private readonly IEventBus _eventBus = eventBus;
    private readonly IStateStore<Order> _orderStore = orderStore;
    private readonly IStateStore<PaymentPendingIndex> _paymentPendingIndexStore = paymentPendingIndexStore;
    private readonly IStateStore<OrderPayIdempotencyRecord> _payIdempotencyStore = payIdempotencyStore;
    private readonly PaymentCallbackOptions _paymentCallbackOptions = paymentCallbackOptions;
    private readonly IUserService _userService = userService;
    private readonly IPaymentGateway _paymentGateway = paymentGateway;
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

        if (!string.IsNullOrWhiteSpace(order.UserId))
        {
            var userGuard = await UserStatusGuard.EnsureUserIsActiveAsync(this, _userService, order.UserId, cancellationToken);
            if (userGuard is not null)
            {
                return userGuard;
            }
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

        var capture = await _paymentGateway.CaptureAsync(order.Id, order.FinalAmount, idemKey, cancellationToken);
        if (!capture.Success || string.IsNullOrWhiteSpace(capture.TransactionId))
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.UpstreamError, "Payment capture failed.", new { capture.Error })));
        }

        var now = DateTimeOffset.UtcNow;
        order.Status = OrderStatus.Confirmed;
        order.PaidAt = now;
        order.PaymentTransactionId = capture.TransactionId;
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
                    var inventoryProductId = string.IsNullOrWhiteSpace(line.SkuId) ? line.ProductId : $"{line.ProductId}::{line.SkuId.Trim()}";
                    await _inventoryService.ReleaseAsync(inventoryProductId, new InventoryQuantityRequest(line.Quantity));
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

    [AllowAnonymous]
    [HttpPost("payments/callback")]
    public async Task<IActionResult> PaymentCallback(
        [FromBody] PaymentCallbackRequest request,
        [FromHeader(Name = "x-payment-signature")] string? signature,
        [FromHeader(Name = "x-payment-timestamp")] string? timestamp,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CallbackId)
            || string.IsNullOrWhiteSpace(request.OrderId)
            || string.IsNullOrWhiteSpace(request.TransactionId)
            || string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidRequest, "Missing callback fields.")));
        }

        if (!IsCallbackTimestampValid(timestamp, out var tsEpochSeconds))
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidSignature, "Invalid callback timestamp.")));
        }

        if (!VerifyCallbackSignature(request, signature, tsEpochSeconds))
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidSignature, "Invalid callback signature.")));
        }

        var callbackId = request.CallbackId.Trim();
        var callbackIdemKey = BuildCallbackIdempotencyStateKey(callbackId);
        var prior = await _payIdempotencyStore.GetAsync(callbackIdemKey, cancellationToken);
        if (prior is not null)
        {
            return Ok(new ApiResponse<object>(true, new { accepted = true, idempotent = true }, null));
        }

        var orderId = request.OrderId.Trim();
        var order = await _orderStore.GetAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Order not found.")));
        }

        if (!string.Equals(request.Status.Trim(), "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            var callbackStatus = request.Status.Trim().ToLowerInvariant();
            if (callbackStatus is "failed" or "cancelled")
            {
                if (order.Status != OrderStatus.AwaitingPayment)
                {
                    await _payIdempotencyStore.SaveAsync(callbackIdemKey, new OrderPayIdempotencyRecord { OrderId = order.Id, ProcessedAt = DateTimeOffset.UtcNow }, cancellationToken);
                    return Ok(new ApiResponse<object>(true, new { accepted = true, idempotent = true }, null));
                }

                try
                {
                    foreach (var line in order.SubOrders.SelectMany(s => s.Lines))
                    {
                        var inventoryProductId = string.IsNullOrWhiteSpace(line.SkuId) ? line.ProductId : $"{line.ProductId}::{line.SkuId.Trim()}";
                        await _inventoryService.ReleaseAsync(inventoryProductId, new InventoryQuantityRequest(line.Quantity));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Inventory release failed while handling payment callback failure. OrderId={OrderId}", order.Id);
                    return StatusCode(StatusCodes.Status502BadGateway,
                        new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.UpstreamError, "Inventory compensation failed for payment callback.")));
                }

                var callbackNow = DateTimeOffset.UtcNow;
                foreach (var sub in order.SubOrders)
                {
                    SubOrderFulfillmentHelper.Normalize(sub);
                    sub.FulfillmentStatus = SubOrderFulfillmentStatus.Cancelled;
                    sub.CancelledAt = callbackNow;
                }

                order.Status = OrderStatus.Failed;
                order.FailureReason = $"Payment callback reported {callbackStatus}.";
                order.UpdatedAt = callbackNow;
                await _orderStore.SaveAsync(order.Id, order, cancellationToken);
                await PaymentPendingIndexHelper.RemoveOrderIdAsync(_paymentPendingIndexStore, order.Id, cancellationToken);
                await _payIdempotencyStore.SaveAsync(callbackIdemKey, new OrderPayIdempotencyRecord { OrderId = order.Id, ProcessedAt = callbackNow }, cancellationToken);
                await _eventBus.PublishAsync(
                    new OrderPaymentFailedEvent(order.Id, order.UserId, callbackStatus, request.TransactionId?.Trim(), order.FailureReason, callbackNow),
                    topic: "order.payment.failed",
                    cancellationToken: cancellationToken);
                await _eventBus.PublishAsync(
                    new OrderCancelledEvent(order.Id, order.UserId, FullOrder: true, SubOrderId: null, callbackNow),
                    topic: "order.cancelled",
                    cancellationToken: cancellationToken);
                return Ok(new ApiResponse<object>(true, new { accepted = true, status = callbackStatus }, null));
            }

            return Ok(new ApiResponse<object>(true, new { accepted = true, ignored = true, reason = "status_not_supported" }, null));
        }

        if (order.Status == OrderStatus.Confirmed)
        {
            await _payIdempotencyStore.SaveAsync(callbackIdemKey, new OrderPayIdempotencyRecord { OrderId = order.Id, ProcessedAt = DateTimeOffset.UtcNow }, cancellationToken);
            return Ok(new ApiResponse<object>(true, new { accepted = true, idempotent = true }, null));
        }

        if (order.Status != OrderStatus.AwaitingPayment)
        {
            return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.PaymentRequired, "Order is not awaiting payment.", new { order.Status })));
        }

        var expectedAmount = Math.Round(order.FinalAmount, 2);
        var callbackAmount = Math.Round(request.Amount, 2);
        if (expectedAmount != callbackAmount)
        {
            return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Conflict, "Callback amount does not match order amount.", new { expectedAmount, callbackAmount })));
        }

        var now = DateTimeOffset.UtcNow;
        order.Status = OrderStatus.Confirmed;
        order.PaidAt = request.PaidAt == default ? now : request.PaidAt;
        order.PaymentTransactionId = request.TransactionId.Trim();
        order.UpdatedAt = now;
        await _orderStore.SaveAsync(order.Id, order, cancellationToken);
        await PaymentPendingIndexHelper.RemoveOrderIdAsync(_paymentPendingIndexStore, order.Id, cancellationToken);
        await _payIdempotencyStore.SaveAsync(callbackIdemKey, new OrderPayIdempotencyRecord { OrderId = order.Id, ProcessedAt = now }, cancellationToken);

        await _eventBus.PublishAsync(
            new OrderPaidEvent(order.Id, order.UserId, now),
            topic: "order.paid",
            cancellationToken: cancellationToken);

        return Ok(new ApiResponse<object>(true, new { accepted = true }, null));
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

    private static string BuildCallbackIdempotencyStateKey(string callbackId)
        => $"order:pay-callback-idem:{callbackId}";

    private bool IsCallbackTimestampValid(string? timestamp, out long tsEpochSeconds)
    {
        tsEpochSeconds = 0;
        if (!long.TryParse(timestamp, out tsEpochSeconds))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var diff = Math.Abs(now - tsEpochSeconds);
        return diff <= Math.Max(30, _paymentCallbackOptions.AllowedClockSkewSeconds);
    }

    private bool VerifyCallbackSignature(PaymentCallbackRequest request, string? signature, long tsEpochSeconds)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var canonical = string.Join("\n",
            tsEpochSeconds.ToString(CultureInfo.InvariantCulture),
            request.CallbackId.Trim(),
            request.OrderId.Trim(),
            request.TransactionId.Trim(),
            Math.Round(request.Amount, 2).ToString("F2", CultureInfo.InvariantCulture),
            request.Status.Trim().ToLowerInvariant(),
            request.PaidAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_paymentCallbackOptions.SharedSecret));
        var computedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        var computedHex = Convert.ToHexString(computedBytes).ToLowerInvariant();
        var incomingHex = signature.Trim().ToLowerInvariant();
        if (computedHex.Length != incomingHex.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(incomingHex));
    }
}
