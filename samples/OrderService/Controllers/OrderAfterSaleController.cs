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
public sealed class OrderAfterSaleController(
    IEventBus eventBus,
    IStateStore<Order> orderStore,
    IStateStore<ShopOrderIndex> shopOrderIndexStore,
    IStateStore<OrderRefundIdempotencyRecord> refundIdempotencyStore,
    IStateStore<RefundLedgerIndex> refundLedgerStore,
    IUserService userService,
    IPaymentGateway paymentGateway) : ControllerBase
{
    [HttpGet("{orderId}/after-sales")]
    public async Task<IActionResult> ListAfterSales(string orderId, CancellationToken cancellationToken)
    {
        var order = await orderStore.GetAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Order not found.")));
        }

        if (!OrderAccess.CanAccessOrder(User, order))
        {
            return OrderAccess.Forbidden();
        }

        return Ok(new ApiResponse<IReadOnlyList<AfterSaleRequest>>(true, order.AfterSales.OrderByDescending(x => x.RequestedAt).ToList(), null));
    }

    [HttpPost("{orderId}/after-sales")]
    public async Task<IActionResult> CreateAfterSale(string orderId, [FromBody] CreateAfterSaleRequest request, CancellationToken cancellationToken)
    {
        var order = await orderStore.GetAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Order not found.")));
        }

        if (!OrderAccess.CanAccessOrder(User, order))
        {
            return OrderAccess.Forbidden();
        }

        var userId = OrderAccess.GetUserId(User);
        if (userId is null)
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Missing user identity.")));
        }

        var userGuard = await UserStatusGuard.EnsureUserIsActiveAsync(this, userService, userId, cancellationToken);
        if (userGuard is not null)
        {
            return userGuard;
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidAfterSale, "Reason is required.")));
        }

        if (!string.IsNullOrWhiteSpace(request.SubOrderId)
            && !order.SubOrders.Any(x => string.Equals(x.Id, request.SubOrderId, StringComparison.Ordinal)))
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.SubOrderNotFound, "Sub-order not found.")));
        }

        var afterSale = new AfterSaleRequest
        {
            SubOrderId = string.IsNullOrWhiteSpace(request.SubOrderId) ? null : request.SubOrderId.Trim(),
            Reason = request.Reason.Trim(),
            Detail = string.IsNullOrWhiteSpace(request.Detail) ? null : request.Detail.Trim(),
            RequestedAmount = request.RequestedAmount,
            RequestedByUserId = userId,
            RequestedAt = DateTimeOffset.UtcNow,
            Status = AfterSaleStatus.Pending,
            RefundStatus = request.RequestedAmount is > 0 ? AfterSaleRefundStatus.Pending : AfterSaleRefundStatus.NotRequired
        };

        order.AfterSales.Add(afterSale);
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await orderStore.SaveAsync(order.Id, order, cancellationToken);

        await eventBus.PublishAsync(
            new AfterSaleRequestedEvent(order.Id, afterSale.Id, userId, afterSale.SubOrderId, afterSale.Reason, afterSale.RequestedAmount, afterSale.RequestedAt),
            topic: "order.aftersale.requested",
            cancellationToken: cancellationToken);

        return Ok(new ApiResponse<AfterSaleRequest>(true, afterSale, null));
    }

    [HttpGet("by-shop/{shopId}/after-sales")]
    public async Task<IActionResult> ListAfterSalesByShop(string shopId, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId))
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidRequest, "ShopId is required.")));
        }

        var normalizedShopId = shopId.Trim();
        if (!OrderAccess.CanAccessShop(User, normalizedShopId))
        {
            return OrderAccess.Forbidden();
        }

        var safeTake = Math.Clamp(take, 1, 100);
        var index = await shopOrderIndexStore.GetAsync(ShopOrderIndex.StateKey(normalizedShopId), cancellationToken);
        if (index?.OrderIds is null || index.OrderIds.Count == 0)
        {
            return Ok(new ApiResponse<IReadOnlyList<MerchantAfterSaleView>>(true, [], null));
        }

        var result = new List<MerchantAfterSaleView>();
        foreach (var orderId in index.OrderIds.Take(safeTake))
        {
            var order = await orderStore.GetAsync(orderId, cancellationToken);
            if (order is null || order.AfterSales.Count == 0)
            {
                continue;
            }

            result.AddRange(ProjectAfterSalesForShop(order, normalizedShopId));
        }

        return Ok(new ApiResponse<IReadOnlyList<MerchantAfterSaleView>>(true, result.OrderByDescending(x => x.RequestedAt).ToList(), null));
    }

    [HttpGet("by-shop/{shopId}/after-sales/search")]
    public async Task<IActionResult> SearchAfterSalesByShop(
        string shopId,
        [FromQuery] string? status,
        [FromQuery] string? refundStatus,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId))
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidRequest, "ShopId is required.")));
        }

        var normalizedShopId = shopId.Trim();
        if (!OrderAccess.CanAccessShop(User, normalizedShopId))
        {
            return OrderAccess.Forbidden();
        }

        var index = await shopOrderIndexStore.GetAsync(ShopOrderIndex.StateKey(normalizedShopId), cancellationToken);
        if (index?.OrderIds is null || index.OrderIds.Count == 0)
        {
            var empty = new PagedResult<MerchantAfterSaleView>([], 1, 20, 0);
            return Ok(new ApiResponse<PagedResult<MerchantAfterSaleView>>(true, empty, null));
        }

        var all = new List<MerchantAfterSaleView>();
        foreach (var orderId in index.OrderIds)
        {
            var order = await orderStore.GetAsync(orderId, cancellationToken);
            if (order is null || order.AfterSales.Count == 0)
            {
                continue;
            }

            all.AddRange(ProjectAfterSalesForShop(order, normalizedShopId));
        }

        IEnumerable<MerchantAfterSaleView> filtered = all;
        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(x => string.Equals(x.Status, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(refundStatus))
        {
            filtered = filtered.Where(x => string.Equals(x.RefundStatus, refundStatus.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (from.HasValue)
        {
            filtered = filtered.Where(x => x.RequestedAt >= from.Value);
        }

        if (to.HasValue)
        {
            filtered = filtered.Where(x => x.RequestedAt <= to.Value);
        }

        var q = new PageQuery(page, pageSize);
        var ordered = filtered.OrderByDescending(x => x.RequestedAt).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((q.SafePage - 1) * q.SafePageSize).Take(q.SafePageSize).ToList();
        var data = new PagedResult<MerchantAfterSaleView>(items, q.SafePage, q.SafePageSize, total);
        return Ok(new ApiResponse<PagedResult<MerchantAfterSaleView>>(true, data, null));
    }

    [HttpPost("{orderId}/after-sales/{afterSaleId}/approve")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ApproveAfterSale(string orderId, string afterSaleId, [FromBody] ReviewAfterSaleRequest? request, CancellationToken cancellationToken)
    {
        return await ReviewAfterSaleAsync(orderId, afterSaleId, AfterSaleStatus.Approved, request?.Note, cancellationToken);
    }

    [HttpPost("{orderId}/after-sales/{afterSaleId}/reject")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RejectAfterSale(string orderId, string afterSaleId, [FromBody] ReviewAfterSaleRequest? request, CancellationToken cancellationToken)
    {
        return await ReviewAfterSaleAsync(orderId, afterSaleId, AfterSaleStatus.Rejected, request?.Note, cancellationToken);
    }

    private async Task<IActionResult> ReviewAfterSaleAsync(
        string orderId,
        string afterSaleId,
        string targetStatus,
        string? note,
        CancellationToken cancellationToken)
    {
        var order = await orderStore.GetAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Order not found.")));
        }

        var entry = order.AfterSales.FirstOrDefault(x => string.Equals(x.Id, afterSaleId, StringComparison.Ordinal));
        if (entry is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "After-sale request not found.")));
        }

        if (!string.Equals(entry.Status, AfterSaleStatus.Pending, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(targetStatus, AfterSaleStatus.Approved, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.Status, AfterSaleStatus.Approved, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new ApiResponse<AfterSaleRequest>(true, entry, null));
            }

            return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidAfterSaleState, "After-sale request is already reviewed.", new { entry.Status })));
        }

        var reviewer = OrderAccess.GetUserId(User) ?? "admin";
        entry.Status = targetStatus;
        entry.DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        entry.ReviewedByUserId = reviewer;
        entry.ReviewedAt = DateTimeOffset.UtcNow;

        if (string.Equals(targetStatus, AfterSaleStatus.Approved, StringComparison.OrdinalIgnoreCase))
        {
            var requestedAmount = Math.Round(Math.Max(0, entry.RequestedAmount ?? 0), 2);
            if (requestedAmount > 0)
            {
                if (order.PaidAt is null || string.IsNullOrWhiteSpace(order.PaymentTransactionId))
                {
                    entry.RefundStatus = AfterSaleRefundStatus.Failed;
                    return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidAfterSaleState, "Cannot refund because order payment was not captured.", new { order.PaidAt, order.PaymentTransactionId })));
                }

                var idemKey = BuildRefundIdempotencyStateKey(order.Id, entry.Id);
                var prior = await refundIdempotencyStore.GetAsync(idemKey, cancellationToken);
                if (prior is not null)
                {
                    entry.RefundStatus = AfterSaleRefundStatus.Succeeded;
                    entry.RefundTransactionId = prior.RefundTransactionId;
                    entry.RefundedAt = prior.ProcessedAt;
                    entry.RefundedAmount = prior.Amount;
                }
                else
                {
                var refund = await paymentGateway.RefundAsync(order.Id, order.PaymentTransactionId, requestedAmount, entry.Reason, cancellationToken);
                if (!refund.Success || string.IsNullOrWhiteSpace(refund.RefundTransactionId))
                {
                    entry.RefundStatus = AfterSaleRefundStatus.Failed;
                    return StatusCode(StatusCodes.Status502BadGateway, new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.UpstreamError, "Refund failed.", new
                    {
                        refund.Error,
                        refund.ErrorCode,
                        refund.Retryable,
                        refund.Gateway
                    })));
                }

                entry.RefundStatus = AfterSaleRefundStatus.Succeeded;
                entry.RefundTransactionId = refund.RefundTransactionId;
                entry.RefundedAt = DateTimeOffset.UtcNow;
                entry.RefundedAmount = requestedAmount;

                    await refundIdempotencyStore.SaveAsync(
                        idemKey,
                        new OrderRefundIdempotencyRecord
                        {
                            OrderId = order.Id,
                            AfterSaleId = entry.Id,
                            RefundTransactionId = refund.RefundTransactionId,
                            Amount = requestedAmount,
                            ProcessedAt = entry.RefundedAt.Value
                        },
                        cancellationToken);
                }
            }
            else
            {
                entry.RefundStatus = AfterSaleRefundStatus.NotRequired;
            }
        }
        else
        {
            entry.RefundStatus = AfterSaleRefundStatus.NotRequired;
        }

        order.UpdatedAt = DateTimeOffset.UtcNow;

        await orderStore.SaveAsync(order.Id, order, cancellationToken);
        await eventBus.PublishAsync(
            new AfterSaleReviewedEvent(order.Id, entry.Id, entry.Status, reviewer, entry.ReviewedAt.Value, entry.DecisionNote),
            topic: "order.aftersale.reviewed",
            cancellationToken: cancellationToken);
        if (string.Equals(entry.RefundStatus, AfterSaleRefundStatus.Succeeded, StringComparison.OrdinalIgnoreCase)
            && entry.RefundedAmount.HasValue
            && !string.IsNullOrWhiteSpace(entry.RefundTransactionId)
            && entry.RefundedAt.HasValue)
        {
            await AppendRefundLedgerAsync(order.Id, entry, cancellationToken);
            await eventBus.PublishAsync(
                new OrderRefundedEvent(order.Id, entry.Id, order.UserId, entry.RefundedAmount.Value, entry.RefundTransactionId, entry.RefundedAt.Value),
                topic: "order.refunded",
                cancellationToken: cancellationToken);
        }

        return Ok(new ApiResponse<AfterSaleRequest>(true, entry, null));
    }

    private async Task AppendRefundLedgerAsync(string orderId, AfterSaleRequest entry, CancellationToken cancellationToken)
    {
        var ledger = await refundLedgerStore.GetAsync(RefundLedgerIndex.StateKey, cancellationToken) ?? new RefundLedgerIndex();
        if (ledger.Entries.Any(x => string.Equals(x.OrderId, orderId, StringComparison.Ordinal) && string.Equals(x.AfterSaleId, entry.Id, StringComparison.Ordinal)))
        {
            return;
        }

        ledger.Entries.Insert(0, new RefundLedgerEntry
        {
            OrderId = orderId,
            AfterSaleId = entry.Id,
            RefundTransactionId = entry.RefundTransactionId!,
            Amount = entry.RefundedAmount ?? 0,
            RefundedAt = entry.RefundedAt ?? DateTimeOffset.UtcNow
        });
        if (ledger.Entries.Count > 5000)
        {
            ledger.Entries = ledger.Entries.Take(5000).ToList();
        }

        await refundLedgerStore.SaveAsync(RefundLedgerIndex.StateKey, ledger, cancellationToken);
    }

    private static string BuildRefundIdempotencyStateKey(string orderId, string afterSaleId)
        => $"order:refund-idem:{orderId}:{afterSaleId}";

    private static List<MerchantAfterSaleView> ProjectAfterSalesForShop(Order order, string shopId)
    {
        var subOrderById = order.SubOrders
            .Where(x => string.Equals(x.ShopId, shopId, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.Id, x => x, StringComparer.Ordinal);
        var list = new List<MerchantAfterSaleView>();
        foreach (var afterSale in order.AfterSales)
        {
            if (string.IsNullOrWhiteSpace(afterSale.SubOrderId))
            {
                foreach (var subOrder in subOrderById.Values)
                {
                    list.Add(new MerchantAfterSaleView(
                        OrderId: order.Id,
                        ShopId: subOrder.ShopId,
                        SubOrderId: subOrder.Id,
                        AfterSaleId: afterSale.Id,
                        Status: afterSale.Status,
                        Reason: afterSale.Reason,
                        Detail: afterSale.Detail,
                        RequestedAmount: afterSale.RequestedAmount,
                        RequestedByUserId: afterSale.RequestedByUserId,
                        RequestedAt: afterSale.RequestedAt,
                        RefundStatus: afterSale.RefundStatus,
                        RefundedAmount: afterSale.RefundedAmount,
                        RefundedAt: afterSale.RefundedAt));
                }

                continue;
            }

            if (subOrderById.TryGetValue(afterSale.SubOrderId, out var matched))
            {
                list.Add(new MerchantAfterSaleView(
                    OrderId: order.Id,
                    ShopId: matched.ShopId,
                    SubOrderId: matched.Id,
                    AfterSaleId: afterSale.Id,
                    Status: afterSale.Status,
                    Reason: afterSale.Reason,
                    Detail: afterSale.Detail,
                    RequestedAmount: afterSale.RequestedAmount,
                    RequestedByUserId: afterSale.RequestedByUserId,
                    RequestedAt: afterSale.RequestedAt,
                    RefundStatus: afterSale.RefundStatus,
                    RefundedAmount: afterSale.RefundedAmount,
                    RefundedAt: afterSale.RefundedAt));
            }
        }

        return list;
    }
}
