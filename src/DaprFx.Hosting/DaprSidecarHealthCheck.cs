using Dapr.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DaprFx.Hosting;

internal sealed class DaprSidecarHealthCheck(DaprClient daprClient) : IHealthCheck
{
    private readonly DaprClient _daprClient = daprClient;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = await _daprClient.GetMetadataAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(metadata.Id)
                ? HealthCheckResult.Unhealthy("Dapr metadata is empty.")
                : HealthCheckResult.Healthy($"Dapr app id: {metadata.Id}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Unable to contact Dapr sidecar.", ex);
        }
    }
}
