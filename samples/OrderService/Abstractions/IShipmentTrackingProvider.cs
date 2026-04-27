using OrderService.Models;

namespace OrderService.Abstractions;

public interface IShipmentTrackingProvider
{
    Task<IReadOnlyList<SubOrderTrackingEvent>> QueryAsync(
        string? carrierCode,
        string? trackingNumber,
        IReadOnlyList<SubOrderTrackingEvent> existing,
        CancellationToken cancellationToken);
}
