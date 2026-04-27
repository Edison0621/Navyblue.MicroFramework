using AuditService.Models;
using AuditService.Repositories;
using Dapr;
using Microsoft.AspNetCore.Mvc;

namespace AuditService.Controllers;

[ApiController]
[Route("api/audit")]
public sealed class AuditController(IAuditRepository auditRepository) : ControllerBase
{
    [HttpPost("events")]
    public async Task<IActionResult> CreateAuditEvent([FromBody] AuditEventRequest request, CancellationToken cancellationToken)
    {
        var entry = BuildAuditEvent(request);
        await auditRepository.AppendAsync(entry, cancellationToken);
        return Created($"/api/audit/events/{entry.Id}", new ApiResponse<AuditEvent>(true, entry, null));
    }

    [HttpPost("subscriptions/order-created")]
    [Topic("orderpubsub", "order.created")]
    public async Task<IActionResult> OnOrderCreated([FromBody] OrderCreatedEvent payload, CancellationToken cancellationToken)
    {
        var subDetail = payload.SubOrders is { Count: > 0 } subs
            ? string.Join(";", subs.Select(s => $"{s.ShopId}:{string.Join(',', s.Lines.Select(l => $"{l.ProductId}x{l.Quantity}"))}"))
            : null;
        var detail = subDetail is null
            ? $"productId={payload.ProductId};quantity={payload.Quantity}"
            : $"userId={payload.UserId ?? "-"};subOrders={subDetail};primaryProduct={payload.ProductId};totalQty={payload.Quantity}";
        var request = new AuditEventRequest(
            ActorId: "system",
            Action: "order.created",
            ResourceType: "order",
            ResourceId: payload.OrderId,
            Result: "success",
            Detail: detail);
        var entry = BuildAuditEvent(request);
        await auditRepository.AppendAsync(entry, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "consumed" }, null));
    }

    [HttpPost("subscriptions/order-cancelled")]
    [Topic("orderpubsub", "order.cancelled")]
    public async Task<IActionResult> OnOrderCancelled([FromBody] OrderCancelledEvent payload, CancellationToken cancellationToken)
    {
        var detail = $"fullOrder={payload.FullOrder};subOrderId={payload.SubOrderId ?? "-"};userId={payload.UserId ?? "-"}";
        var request = new AuditEventRequest(
            ActorId: "system",
            Action: "order.cancelled",
            ResourceType: "order",
            ResourceId: payload.OrderId,
            Result: "success",
            Detail: detail);
        var entry = BuildAuditEvent(request);
        await auditRepository.AppendAsync(entry, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "consumed" }, null));
    }

    [HttpPost("subscriptions/order-paid")]
    [Topic("orderpubsub", "order.paid")]
    public async Task<IActionResult> OnOrderPaid([FromBody] OrderPaidEvent payload, CancellationToken cancellationToken)
    {
        var request = new AuditEventRequest(
            ActorId: "system",
            Action: "order.paid",
            ResourceType: "order",
            ResourceId: payload.OrderId,
            Result: "success",
            Detail: $"userId={payload.UserId ?? "-"}");
        var entry = BuildAuditEvent(request);
        await auditRepository.AppendAsync(entry, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "consumed" }, null));
    }

    [HttpPost("subscriptions/order-completed")]
    [Topic("orderpubsub", "order.completed")]
    public async Task<IActionResult> OnOrderCompleted([FromBody] OrderCompletedEvent payload, CancellationToken cancellationToken)
    {
        var request = new AuditEventRequest(
            ActorId: "system",
            Action: "order.completed",
            ResourceType: "order",
            ResourceId: payload.OrderId,
            Result: "success",
            Detail: $"userId={payload.UserId ?? "-"}");
        var entry = BuildAuditEvent(request);
        await auditRepository.AppendAsync(entry, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "consumed" }, null));
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetAuditEvents(
        [FromQuery] string? actorId,
        [FromQuery] string? action,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var query = new PageQuery(page, pageSize);
        var safePage = query.SafePage;
        var safePageSize = query.SafePageSize;
        var entries = await auditRepository.GetAllAsync(cancellationToken);
        var filtered = entries.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(actorId))
        {
            filtered = filtered.Where(x => x.ActorId.Equals(actorId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            filtered = filtered.Where(x => x.Action.Equals(action, StringComparison.OrdinalIgnoreCase));
        }

        if (from.HasValue)
        {
            filtered = filtered.Where(x => x.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            filtered = filtered.Where(x => x.CreatedAt <= to.Value);
        }

        var ordered = filtered.OrderByDescending(x => x.CreatedAt).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList();
        var data = new PagedResult<AuditEvent>(items, safePage, safePageSize, total);
        return Ok(new ApiResponse<PagedResult<AuditEvent>>(true, data, null));
    }

    private static AuditEvent BuildAuditEvent(AuditEventRequest request)
    {
        return new AuditEvent(
            Guid.NewGuid(),
            request.ActorId,
            request.Action,
            request.ResourceType,
            request.ResourceId,
            request.Result,
            request.Detail,
            DateTimeOffset.UtcNow);
    }
}
