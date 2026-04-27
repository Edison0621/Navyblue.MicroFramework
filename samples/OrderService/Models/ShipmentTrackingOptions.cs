namespace OrderService.Models;

public sealed class ShipmentTrackingOptions
{
    public string Provider { get; init; } = "mock";
    public int TimeoutMs { get; init; } = 3000;
    public int RetryCount { get; init; } = 1;
    public bool DeduplicateEvents { get; init; } = true;
    public string SourceName { get; init; } = "mock-carrier";
}
