namespace OrderService.Models;

public sealed class SubOrderShipRequest
{
    public string? TrackingNumber { get; set; }
    public string? CarrierCode { get; set; }
    public string? CarrierName { get; set; }
}
