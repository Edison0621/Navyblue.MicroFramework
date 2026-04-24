namespace DaprFx.Operations;

public interface IOpsDashboardContributor
{
    string SectionName { get; }
    Task<object?> BuildAsync(CancellationToken cancellationToken = default);
}
