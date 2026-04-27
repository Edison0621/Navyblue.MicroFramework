using OrderService.Abstractions;
using OrderService.Models;

namespace OrderService.Services;

public sealed class MockShipmentTrackingProvider : IShipmentTrackingProvider
{
    private readonly ShipmentTrackingOptions _options;

    public MockShipmentTrackingProvider(ShipmentTrackingOptions options)
    {
        _options = options;
    }

    public Task<IReadOnlyList<SubOrderTrackingEvent>> QueryAsync(
        string? carrierCode,
        string? trackingNumber,
        IReadOnlyList<SubOrderTrackingEvent> existing,
        CancellationToken cancellationToken)
    {
        var result = existing
            .OrderBy(x => x.CreatedAt)
            .ToList();
        if (!string.IsNullOrWhiteSpace(trackingNumber) && result.Count == 1)
        {
            result.Add(new SubOrderTrackingEvent
            {
                Status = "InTransit",
                Message = "Package is in transit (mock).",
                Source = _options.SourceName,
                TrackingNumber = trackingNumber,
                CarrierCode = carrierCode,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        if (_options.DeduplicateEvents)
        {
            result = result
                .GroupBy(x => $"{x.Status}|{x.Message}|{x.Source}|{x.TrackingNumber}|{x.CarrierCode}|{x.CarrierName}|{x.CreatedAt:O}", StringComparer.Ordinal)
                .Select(x => x.First())
                .OrderBy(x => x.CreatedAt)
                .ToList();
        }

        return Task.FromResult<IReadOnlyList<SubOrderTrackingEvent>>(result);
    }
}
