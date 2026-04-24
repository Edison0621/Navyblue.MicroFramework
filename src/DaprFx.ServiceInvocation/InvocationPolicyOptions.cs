namespace DaprFx.ServiceInvocation;

public sealed class InvocationPolicyOptions
{
    public int MaxRetries { get; set; } = 2;
    public int TimeoutSeconds { get; set; } = 10;
    public int FailureThreshold { get; set; } = 5;
    public int OpenSeconds { get; set; } = 30;
}
