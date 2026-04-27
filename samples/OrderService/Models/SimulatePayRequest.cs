namespace OrderService.Models;

public sealed class SimulatePayRequest
{
    public string? IdempotencyKey { get; set; }
}
