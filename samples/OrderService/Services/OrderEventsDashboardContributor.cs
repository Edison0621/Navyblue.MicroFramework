using DaprFx.Operations;

namespace OrderService.Services;

public sealed class OrderEventsDashboardContributor(EventAuditStore eventAuditStore) : IOpsDashboardContributor
{
    private readonly EventAuditStore _eventAuditStore = eventAuditStore;
    public string SectionName => "events";

    public Task<object?> BuildAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var events = _eventAuditStore.List(200);

        var topicStats = events
            .GroupBy(e => e.Topic)
            .Select(group => new
            {
                Topic = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToArray();

        var traceStats = events
            .Where(e => e.ReceivedAt >= now.AddDays(-7))
            .GroupBy(e => e.ReceivedAt.UtcDateTime.Date)
            .Select(group => new
            {
                Day = group.Key.ToString("MM-dd"),
                Count = group.Count()
            })
            .OrderBy(x => x.Day)
            .ToArray();

        return Task.FromResult<object?>(new
        {
            Total = events.Count,
            LastHour = events.Count(e => e.ReceivedAt >= now.AddHours(-1)),
            Topics = topicStats,
            TraceByDay = traceStats
        });
    }
}
