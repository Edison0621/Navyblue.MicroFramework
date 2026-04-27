using DaprFx.Core;
using Microsoft.AspNetCore.Mvc;
using OrderService.Models;

namespace OrderService.Controllers;

[ApiController]
[Route("ops/outbox/deadletters")]
public sealed class OutboxOpsController(IOutboxOperations outboxOperations) : ControllerBase
{
    private readonly IOutboxOperations _outboxOperations = outboxOperations;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] string? topic = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = new PageQuery(page, pageSize);
        var items = await _outboxOperations.ListDeadLettersAsync(query.SafePage * query.SafePageSize, topic, from, to, cancellationToken);
        var materialized = items.ToList();
        var total = materialized.Count;
        var paged = materialized.Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize).ToList();
        var data = new PagedResult<object>(paged, query.SafePage, query.SafePageSize, total);
        return Ok(new ApiResponse<PagedResult<object>>(true, data, null));
    }

    [HttpPost("replay-all")]
    public async Task<IActionResult> ReplayAll(
        [FromQuery] bool dryRun = false,
        [FromQuery] string? topic = null,
        CancellationToken cancellationToken = default)
    {
        var count = await _outboxOperations.ReplayAllDeadLettersAsync(dryRun, topic, cancellationToken);
        return Accepted(new ApiResponse<object>(true, new { DryRun = dryRun, Topic = topic, Count = count }, null));
    }

    [HttpPost("{messageId:guid}/requeue")]
    public async Task<IActionResult> Requeue(Guid messageId, CancellationToken cancellationToken = default)
    {
        await _outboxOperations.RequeueDeadLetterAsync(messageId, cancellationToken);
        return Accepted(new ApiResponse<object>(true, new { MessageId = messageId, Action = "Requeued" }, null));
    }

    [HttpDelete("{messageId:guid}")]
    public async Task<IActionResult> Delete(Guid messageId, CancellationToken cancellationToken = default)
    {
        await _outboxOperations.DeleteDeadLetterAsync(messageId, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { MessageId = messageId, Action = "Deleted" }, null));
    }
}
