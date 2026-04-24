namespace DaprFx.Core;

public sealed class ServiceClientOptions
{
    public LoadBalancingStrategy LoadBalancingStrategy { get; set; } = LoadBalancingStrategy.RoundRobin;
    public int? InvocationMaxRetries { get; set; }
    public int? InvocationTimeoutSeconds { get; set; }
    public int? CircuitBreakerFailureThreshold { get; set; }
    public int? CircuitBreakerOpenSeconds { get; set; }
}
