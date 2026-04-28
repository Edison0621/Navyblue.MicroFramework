using JobService.Models;
using JobService.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace JobService.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(IJobRunRepository jobRunRepository, IHttpClientFactory httpClientFactory) : ControllerBase
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(150),
        TimeSpan.FromMilliseconds(300)
    ];
    private const string OrderServiceClientName = "orderservice";
    private const string InventoryServiceClientName = "inventoryservice";
    private const string CatalogServiceClientName = "catalogservice";
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
        var (success, truncated, statusCode) = await ExecuteWithRetryAsync(
            OrderServiceClientName,
            $"api/orders/ops/expire-awaiting-payments?maxAgeMinutes={safeMinutes}",
            cancellationToken);
        var run = new JobRun(
            Guid.NewGuid().ToString("N"),
            "expire-awaiting-payments",
            success ? "success" : "failed",
            success ? truncated : $"HTTP {statusCode}: {truncated}",
            DateTimeOffset.UtcNow);
        await jobRunRepository.AppendAsync(run, cancellationToken);
        return Accepted($"/api/jobs/runs/{run.Id}", new ApiResponse<JobRun>(true, run, null));
    }

    [HttpPost("run/reconcile-refunds")]
    public async Task<IActionResult> RunReconcileRefunds([FromQuery] int take = 200, CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, 1000);
        var (success, truncated, statusCode) = await ExecuteWithRetryAsync(
            OrderServiceClientName,
            $"api/orders/ops/reconcile-refunds?take={safeTake}",
            cancellationToken);
        var run = new JobRun(
            Guid.NewGuid().ToString("N"),
            "reconcile-refunds",
            success ? "success" : "failed",
            success ? truncated : $"HTTP {statusCode}: {truncated}",
            DateTimeOffset.UtcNow);
        await jobRunRepository.AppendAsync(run, cancellationToken);
        return Accepted($"/api/jobs/runs/{run.Id}", new ApiResponse<JobRun>(true, run, null));
    }

    [HttpPost("run/reclaim-expired-inventory-reservations")]
    public async Task<IActionResult> RunReclaimExpiredInventoryReservations([FromQuery] int take = 200, CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, 2000);
        var (success, truncated, statusCode) = await ExecuteWithRetryAsync(
            InventoryServiceClientName,
            $"api/inventory/ops/reclaim-expired-reservations?take={safeTake}",
            cancellationToken);
        var run = new JobRun(
            Guid.NewGuid().ToString("N"),
            "reclaim-expired-inventory-reservations",
            success ? "success" : "failed",
            success ? truncated : $"HTTP {statusCode}: {truncated}",
            DateTimeOffset.UtcNow);
        await jobRunRepository.AppendAsync(run, cancellationToken);
        return Accepted($"/api/jobs/runs/{run.Id}", new ApiResponse<JobRun>(true, run, null));
    }

    [HttpPost("run/apply-catalog-shelf-schedules")]
    public async Task<IActionResult> RunApplyCatalogShelfSchedules([FromQuery] int take = 500, CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, 2000);
        var (success, truncated, statusCode) = await ExecuteWithRetryAsync(
            CatalogServiceClientName,
            $"api/catalog/ops/apply-shelf-schedules?take={safeTake}",
            cancellationToken);
        var run = new JobRun(
            Guid.NewGuid().ToString("N"),
            "apply-catalog-shelf-schedules",
            success ? "success" : "failed",
            success ? truncated : $"HTTP {statusCode}: {truncated}",
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

    private async Task<(bool Success, string Body, int StatusCode)> ExecuteWithRetryAsync(
        string clientName,
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(clientName);
        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            using var response = await client.PostAsync(relativeUrl, content: null, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var truncated = body.Length > 2000 ? body[..2000] : body;
            if (response.IsSuccessStatusCode || (int)response.StatusCode < 500 || attempt == RetryDelays.Length)
            {
                return (response.IsSuccessStatusCode, truncated, (int)response.StatusCode);
            }

            await Task.Delay(RetryDelays[attempt], cancellationToken);
        }

        return (false, "No response.", 502);
    }
}
