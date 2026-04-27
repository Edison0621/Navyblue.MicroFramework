using JobService.Models;

namespace JobService.Repositories;

public interface IJobRunRepository
{
    Task AppendAsync(JobRun item, CancellationToken cancellationToken);
    Task<List<JobRun>> GetRecentAsync(int take, CancellationToken cancellationToken);
}
