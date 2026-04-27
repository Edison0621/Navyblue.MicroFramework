using JobService.Models;
using JobService.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace JobService.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(IJobRunRepository jobRunRepository, IHttpClientFactory httpClientFactory) : ControllerBase
{
    private const string OrderServiceClientName = "orderservice";
    private const string InventoryServiceClientName = "inventoryservice";
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

    [HttpPost("run/expire-awaiting-payments")]
    public async Task<IActionResult> RunExpireAwaitingPayments([FromQuery] int maxAgeMinutes = 30, CancellationToken cancellationToken = default)
    {
        var safeMinutes = Math.Clamp(maxAgeMinutes, 5, 24 * 60);
        var client = httpClientFactory.CreateClient(OrderServiceClientName);
        using var response = await client.PostAsync(
            $"api/orders/ops/expire-awaiting-payments?maxAgeMinutes={safeMinutes}",
            content: null,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var truncated = body.Length > 2000 ? body[..2000] : body;
        var success = response.IsSuccessStatusCode;
        var run = new JobRun(
            Guid.NewGuid().ToString("N"),
            "expire-awaiting-payments",
            success ? "success" : "failed",
            success ? truncated : $"HTTP {(int)response.StatusCode}: {truncated}",
            DateTimeOffset.UtcNow);
        await jobRunRepository.AppendAsync(run, cancellationToken);
        return Accepted($"/api/jobs/runs/{run.Id}", new ApiResponse<JobRun>(true, run, null));
    }

    [HttpPost("run/reconcile-refunds")]
    public async Task<IActionResult> RunReconcileRefunds([FromQuery] int take = 200, CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, 1000);
        var client = httpClientFactory.CreateClient(OrderServiceClientName);
        using var response = await client.PostAsync(
            $"api/orders/ops/reconcile-refunds?take={safeTake}",
            content: null,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var truncated = body.Length > 2000 ? body[..2000] : body;
        var success = response.IsSuccessStatusCode;
        var run = new JobRun(
            Guid.NewGuid().ToString("N"),
            "reconcile-refunds",
            success ? "success" : "failed",
            success ? truncated : $"HTTP {(int)response.StatusCode}: {truncated}",
            DateTimeOffset.UtcNow);
        await jobRunRepository.AppendAsync(run, cancellationToken);
        return Accepted($"/api/jobs/runs/{run.Id}", new ApiResponse<JobRun>(true, run, null));
    }

    [HttpPost("run/reclaim-expired-inventory-reservations")]
    public async Task<IActionResult> RunReclaimExpiredInventoryReservations([FromQuery] int take = 200, CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, 2000);
        var client = httpClientFactory.CreateClient(InventoryServiceClientName);
        using var response = await client.PostAsync(
            $"api/inventory/ops/reclaim-expired-reservations?take={safeTake}",
            content: null,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var truncated = body.Length > 2000 ? body[..2000] : body;
        var success = response.IsSuccessStatusCode;
        var run = new JobRun(
            Guid.NewGuid().ToString("N"),
            "reclaim-expired-inventory-reservations",
            success ? "success" : "failed",
            success ? truncated : $"HTTP {(int)response.StatusCode}: {truncated}",
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
