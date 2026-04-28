using Dapr.Client;
using JobService.Models;

namespace JobService.Repositories;

public sealed class DaprJobRunRepository(DaprClient daprClient, ILogger<DaprJobRunRepository> logger) : IJobRunRepository
{
    private const string StateStoreName = "statestore";
    private const string JobsStateKey = "jobs:history";
    private const int MaxJobHistory = 500;

    public async Task AppendAsync(JobRun item, CancellationToken cancellationToken)
    {
        var items = await GetItemsAsync(cancellationToken);
        items.Add(item);
        if (items.Count > MaxJobHistory)
        {
            items = items.OrderByDescending(x => x.StartedAt).Take(MaxJobHistory).OrderBy(x => x.StartedAt).ToList();
        }

        await SaveItemsAsync(items, cancellationToken);
    }

    public async Task<List<JobRun>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        var safeTake = take <= 0 ? 50 : Math.Min(take, 200);
        var items = await GetItemsAsync(cancellationToken);
        return items.OrderByDescending(x => x.StartedAt).Take(safeTake).ToList();
    }

    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(260), TimeSpan.FromMilliseconds(500)];

    private async Task<List<JobRun>> GetItemsAsync(CancellationToken ct)
    {
        return await ExecuteWithRetryAsync(
            x => daprClient.GetStateAsync<List<JobRun>>(StateStoreName, JobsStateKey, cancellationToken: x),
            "GetJobRuns",
            ct) ?? [];
    }

    private async Task SaveItemsAsync(List<JobRun> items, CancellationToken ct)
    {
        await ExecuteWithRetryAsync(
            x => daprClient.SaveStateAsync(StateStoreName, JobsStateKey, items, cancellationToken: x),
            "SaveJobRuns",
            ct);
    }

    private async Task<TResult> ExecuteWithRetryAsync<TResult>(Func<CancellationToken, Task<TResult>> op, string operation, CancellationToken ct)
    {
        Exception? last = null;
        for (var i = 0; i <= RetryDelays.Length; i++)
        {
            try { return await op(ct); }
            catch (Exception ex) when (ex.GetType().FullName?.Contains("DaprException", StringComparison.Ordinal) == true || ex is HttpRequestException or TimeoutException)
            {
                last = ex;
                logger.LogWarning(ex, "Job history state op {Operation} failed at attempt {Attempt}", operation, i + 1);
                if (i < RetryDelays.Length) await Task.Delay(RetryDelays[i], ct);
            }
        }

        throw new InvalidOperationException($"Job state operation {operation} failed after retries.", last);
    }

    private async Task ExecuteWithRetryAsync(Func<CancellationToken, Task> op, string operation, CancellationToken ct)
    {
        await ExecuteWithRetryAsync<object?>(
            async x =>
            {
                await op(x);
                return null;
            },
            operation,
            ct);
    }
}
