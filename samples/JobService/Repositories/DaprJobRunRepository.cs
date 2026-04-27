using Dapr.Client;
using JobService.Models;

namespace JobService.Repositories;

public sealed class DaprJobRunRepository(DaprClient daprClient) : IJobRunRepository
{
    private const string StateStoreName = "statestore";
    private const string JobsStateKey = "jobs:history";
    private const int MaxJobHistory = 500;

    public async Task AppendAsync(JobRun item, CancellationToken cancellationToken)
    {
        var items = await daprClient.GetStateAsync<List<JobRun>>(StateStoreName, JobsStateKey, cancellationToken: cancellationToken) ?? [];
        items.Add(item);
        if (items.Count > MaxJobHistory)
        {
            items = items.OrderByDescending(x => x.StartedAt).Take(MaxJobHistory).OrderBy(x => x.StartedAt).ToList();
        }

        await daprClient.SaveStateAsync(StateStoreName, JobsStateKey, items, cancellationToken: cancellationToken);
    }

    public async Task<List<JobRun>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        var safeTake = take <= 0 ? 50 : Math.Min(take, 200);
        var items = await daprClient.GetStateAsync<List<JobRun>>(StateStoreName, JobsStateKey, cancellationToken: cancellationToken) ?? [];
        return items.OrderByDescending(x => x.StartedAt).Take(safeTake).ToList();
    }
}
