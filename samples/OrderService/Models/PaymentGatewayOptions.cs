namespace OrderService.Models;

public sealed class PaymentGatewayOptions
{
    public string Provider { get; init; } = "simulated";
    public int TimeoutMs { get; init; } = 5000;
    public int RetryCount { get; init; } = 1;
    public bool SimulateTransientFailure { get; init; } = false;
}
