namespace DaprFx.Operations;

public sealed class OpsDashboardSnapshotBuilder(IEnumerable<IOpsDashboardContributor> contributors)
{
    private readonly IReadOnlyList<IOpsDashboardContributor> _contributors = contributors.ToList();

    public async Task<IReadOnlyDictionary<string, object?>> BuildAsync(CancellationToken cancellationToken = default)
    {
        var sections = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["generatedAt"] = DateTimeOffset.UtcNow
        };

        foreach (var contributor in _contributors)
        {
            var section = await contributor.BuildAsync(cancellationToken);
            if (section is not null)
            {
                sections[contributor.SectionName] = section;
            }
        }

        return sections;
    }
}
