using JobService.Models;
using JobService.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace JobService.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(IJobRunRepository jobRunRepository) : ControllerBase
{
    [HttpPost("run/reconcile-inventory")]
    public async Task<IActionResult> RunReconcileInventory(CancellationToken cancellationToken)
    {
        var run = new JobRun(
            Guid.NewGuid().ToString("N"),
            "reconcile-inventory",
            "success",
            "Skeleton job executed.",
            DateTimeOffset.UtcNow);
        await jobRunRepository.AppendAsync(run, cancellationToken);
        return Accepted($"/api/jobs/runs/{run.Id}", new ApiResponse<JobRun>(true, run, null));
    }

    [HttpPost("run/replay-audit")]
    public async Task<IActionResult> RunReplayAudit(CancellationToken cancellationToken)
    {
        var run = new JobRun(
            Guid.NewGuid().ToString("N"),
            "replay-audit",
            "success",
            "Skeleton replay job executed.",
            DateTimeOffset.UtcNow);
        await jobRunRepository.AppendAsync(run, cancellationToken);
        return Accepted($"/api/jobs/runs/{run.Id}", new ApiResponse<JobRun>(true, run, null));
    }

    [HttpGet("runs")]
    public async Task<IActionResult> GetRuns([FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var query = new PageQuery(page, pageSize);
        var runs = await jobRunRepository.GetRecentAsync(query.SafePage * query.SafePageSize, cancellationToken);
        var total = runs.Count;
        var items = runs.Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize).ToList();
        var data = new PagedResult<JobRun>(items, query.SafePage, query.SafePageSize, total);
        return Ok(new ApiResponse<PagedResult<JobRun>>(true, data, null));
    }
}
