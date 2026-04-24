using DaprFx.Core;
using Microsoft.AspNetCore.Mvc;

namespace OrderService.Controllers;

[ApiController]
[Route("ops/outbox/deadletters")]
public sealed class OutboxOpsController(IOutboxOperations outboxOperations) : ControllerBase
{
    private readonly IOutboxOperations _outboxOperations = outboxOperations;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int take = 100,
        [FromQuery] string? topic = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var items = await _outboxOperations.ListDeadLettersAsync(take, topic, from, to, cancellationToken);
        return Ok(items);
    }

    [HttpPost("replay-all")]
    public async Task<IActionResult> ReplayAll(
        [FromQuery] bool dryRun = false,
        [FromQuery] string? topic = null,
        CancellationToken cancellationToken = default)
    {
        var count = await _outboxOperations.ReplayAllDeadLettersAsync(dryRun, topic, cancellationToken);
        return Accepted(new { DryRun = dryRun, Topic = topic, Count = count });
    }

    [HttpPost("{messageId:guid}/requeue")]
    public async Task<IActionResult> Requeue(Guid messageId, CancellationToken cancellationToken = default)
    {
        await _outboxOperations.RequeueDeadLetterAsync(messageId, cancellationToken);
        return Accepted(new { MessageId = messageId, Action = "Requeued" });
    }

    [HttpDelete("{messageId:guid}")]
    public async Task<IActionResult> Delete(Guid messageId, CancellationToken cancellationToken = default)
    {
        await _outboxOperations.DeleteDeadLetterAsync(messageId, cancellationToken);
        return NoContent();
    }
}
