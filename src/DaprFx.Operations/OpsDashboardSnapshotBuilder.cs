namespace DaprFx.Operations;

public sealed class OpsDashboardSnapshotBuilder(IEnumerable<IOpsDashboardContributor> contributors)
{
    private readonly IReadOnlyList<IOpsDashboardContributor> _contributors = contributors.ToList();
    private sealed record SectionError(string Section, string Error);

    public async Task<IReadOnlyDictionary<string, object?>> BuildAsync(CancellationToken cancellationToken = default)
    {
        var sections = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["generatedAt"] = DateTimeOffset.UtcNow
        };
        var errors = new List<SectionError>();

        foreach (var contributor in _contributors)
        {
            try
            {
                var section = await contributor.BuildAsync(cancellationToken);
                if (section is not null)
                {
                    sections[contributor.SectionName] = section;
                }
            }
            catch (Exception ex)
            {
                errors.Add(new SectionError(contributor.SectionName, ex.Message));
            }
        }

        if (errors.Count > 0)
        {
            sections["errors"] = errors;
        }

        return sections;
    }
}
