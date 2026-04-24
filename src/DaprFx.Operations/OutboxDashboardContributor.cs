using DaprFx.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DaprFx.Operations;

public sealed class OutboxDashboardContributor(IServiceProvider serviceProvider) : IOpsDashboardContributor
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    public string SectionName => "outbox";

    public async Task<object?> BuildAsync(CancellationToken cancellationToken = default)
    {
        var outboxOperations = _serviceProvider.GetService<IOutboxOperations>();
        if (outboxOperations is null)
        {
            return null;
        }

        var deadLetters = await outboxOperations.ListDeadLettersAsync(200, cancellationToken: cancellationToken);
        return new
        {
            DeadLetterCount = deadLetters.Count,
            MaxAttemptCount = deadLetters.Count == 0 ? 0 : deadLetters.Max(x => x.AttemptCount),
            LatestDeadLetterAt = deadLetters.Count == 0 ? (DateTimeOffset?)null : deadLetters.Max(x => x.CreatedAt)
        };
    }
}
