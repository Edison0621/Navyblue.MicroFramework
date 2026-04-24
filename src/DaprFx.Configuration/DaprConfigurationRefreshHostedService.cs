using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DaprFx.Configuration;

internal sealed class DaprConfigurationRefreshHostedService(IConfiguration configuration, ILogger<DaprConfigurationRefreshHostedService> logger) : IHostedService
{
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<DaprConfigurationRefreshHostedService> _logger = logger;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_configuration is not IConfigurationRoot root)
        {
            return Task.CompletedTask;
        }

        foreach (var provider in root.Providers.OfType<DaprConfigurationProvider>())
        {
            provider.StartRefreshLoop(_logger);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
