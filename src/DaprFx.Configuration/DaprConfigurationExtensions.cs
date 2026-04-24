using Microsoft.Extensions.DependencyInjection;

namespace DaprFx.Configuration;

public static class DaprConfigurationExtensions
{
    public static IServiceCollection AddDaprConfiguration(this IServiceCollection services, string configStore, params string[] keys)
    {
        services.AddSingleton(new DaprConfigurationRegistration(configStore, keys.Length == 0 ? ["*"] : keys));
        services.AddHostedService<DaprConfigurationRefreshHostedService>();
        services.AddOptions();
        return services;
    }
}
