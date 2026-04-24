using Dapr.Client;
using DaprFx.Configuration;
using DaprFx.Core;
using DaprFx.Cryptography;
using DaprFx.EventBus;
using DaprFx.ServiceInvocation;
using DaprFx.StateManagement;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Threading;

namespace DaprFx.Hosting;

public static class DaprFxServiceCollectionExtensions
{
    public static WebApplicationBuilder AddDaprMicroFramework(this WebApplicationBuilder builder, Action<DaprFxOptions>? configure = null)
    {
        var options = new DaprFxOptions();
        configure?.Invoke(options);
        builder.Services.AddDaprMicroFramework(_ => CopyOptions(options, _));

        if (!string.IsNullOrWhiteSpace(options.ConfigStore))
        {
            var daprClient = new DaprClientBuilder().Build();
            ((IConfigurationBuilder)builder.Configuration).Add(new DaprConfigurationSource(daprClient, options.ConfigStore, ["*"]));
        }

        return builder;
    }

    public static IServiceCollection AddDaprServiceClient(this IServiceCollection services, Type interfaceType, string appId)
        => AddDaprServiceClient(services, interfaceType, [appId]);

    public static IServiceCollection AddDaprServiceClient(this IServiceCollection services, Type interfaceType, string[] appIds)
        => AddDaprServiceClient(services, interfaceType, appIds, new ServiceClientOptions());

    public static IServiceCollection AddDaprServiceClient(this IServiceCollection services, Type interfaceType, string[] appIds, ServiceClientOptions clientOptions)
    {
        ArgumentNullException.ThrowIfNull(interfaceType);
        ArgumentNullException.ThrowIfNull(appIds);
        ArgumentNullException.ThrowIfNull(clientOptions);
        var filtered = appIds.Where(static s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (filtered.Length == 0)
        {
            throw new ArgumentException("At least one appId is required.", nameof(appIds));
        }

        var cursor = -1;
        services.TryAddTransient(interfaceType, sp =>
        {
            var client = sp.GetRequiredService<DaprClient>();
            var options = sp.GetRequiredService<DaprFxOptions>();
            var selectedAppId = SelectAppId(filtered, clientOptions.LoadBalancingStrategy, ref cursor, sp.GetService<IHttpContextAccessor>());
            var policy = new InvocationPolicyOptions
            {
                MaxRetries = clientOptions.InvocationMaxRetries ?? options.InvocationMaxRetries,
                TimeoutSeconds = clientOptions.InvocationTimeoutSeconds ?? options.InvocationTimeoutSeconds,
                FailureThreshold = clientOptions.CircuitBreakerFailureThreshold ?? options.CircuitBreakerFailureThreshold,
                OpenSeconds = clientOptions.CircuitBreakerOpenSeconds ?? options.CircuitBreakerOpenSeconds
            };
            var method = typeof(DaprServiceClientFactory).GetMethod(nameof(DaprServiceClientFactory.Create), [typeof(DaprClient), typeof(string), typeof(InvocationPolicyOptions)])!
                .MakeGenericMethod(interfaceType);
            return method.Invoke(null, [client, selectedAppId, policy])!;
        });

        return services;
    }

    public static IServiceCollection AddDaprStateStore(this IServiceCollection services, Type entityType, string storeName)
    {
        var stateStoreInterface = typeof(IStateStore<>).MakeGenericType(entityType);
        var stateStoreImplementation = typeof(DaprStateStore<>).MakeGenericType(entityType);
        services.TryAddSingleton(stateStoreInterface, sp =>
            Activator.CreateInstance(stateStoreImplementation, sp.GetRequiredService<DaprClient>(), storeName)!);
        return services;
    }

    public static IServiceCollection AddDaprCryptography(this IServiceCollection services)
    {
        services.TryAddSingleton<ICryptoService, DaprCryptoService>();
        return services;
    }

    public static IServiceCollection AddDaprMicroFramework(this IServiceCollection services, Action<DaprFxOptions>? configure = null)
    {
        var options = new DaprFxOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.AddDaprClient();
        services.AddHttpContextAccessor();
        services.AddControllers().AddDapr();
        services.AddDaprFxObservability(options);
        services.AddDaprFxHealthChecks();

        if (options.UseEventBus && !string.IsNullOrWhiteSpace(options.PubSubName))
        {
            services.AddDaprEventBus(options.PubSubName);
        }

        foreach (var clientRegistration in options.ServiceClients)
        {
            services.AddDaprServiceClient(clientRegistration.InterfaceType, clientRegistration.AppIds, clientRegistration.Options);
        }

        foreach (var (entityType, storeName) in options.StateStores)
        {
            services.AddDaprStateStore(entityType, storeName);
        }

        if (!string.IsNullOrWhiteSpace(options.ConfigStore))
        {
            services.AddDaprConfiguration(options.ConfigStore);
        }

        if (options.UseCryptography)
        {
            services.AddDaprCryptography();
        }

        return services;
    }

    public static IServiceCollection AddDaprFxObservability(this IServiceCollection services, DaprFxOptions? options = null)
    {
        var serviceName = string.IsNullOrWhiteSpace(options?.TelemetryServiceName) ? "DaprFx.Service" : options.TelemetryServiceName;
        var environment = string.IsNullOrWhiteSpace(options?.TelemetryEnvironment) ? "Development" : options.TelemetryEnvironment;
        services.AddOpenTelemetry().WithTracing(builder =>
            {
                builder.ConfigureResource(resource => resource
                    .AddService(serviceName: serviceName, serviceInstanceId: Environment.MachineName)
                    .AddAttributes([new KeyValuePair<string, object>("deployment.environment", environment)]));
                builder.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(options?.OtlpEndpoint))
                {
                    builder.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.OtlpEndpoint));
                }
            })
            .WithMetrics(builder =>
            {
                builder.ConfigureResource(resource => resource
                    .AddService(serviceName: serviceName, serviceInstanceId: Environment.MachineName)
                    .AddAttributes([new KeyValuePair<string, object>("deployment.environment", environment)]));
                builder.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
                if (!string.IsNullOrWhiteSpace(options?.OtlpEndpoint))
                {
                    builder.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.OtlpEndpoint));
                }
            });
        return services;
    }

    public static IServiceCollection AddDaprFxHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy())
            .AddCheck<DaprSidecarHealthCheck>("dapr-sidecar", tags: ["ready"]);
        return services;
    }

    private static void CopyOptions(DaprFxOptions from, DaprFxOptions to)
    {
        to.UseEventBus = from.UseEventBus;
        to.PubSubName = from.PubSubName;
        to.ConfigStore = from.ConfigStore;
        to.UseCryptography = from.UseCryptography;
        to.InvocationMaxRetries = from.InvocationMaxRetries;
        to.InvocationTimeoutSeconds = from.InvocationTimeoutSeconds;
        to.CircuitBreakerFailureThreshold = from.CircuitBreakerFailureThreshold;
        to.CircuitBreakerOpenSeconds = from.CircuitBreakerOpenSeconds;
        to.StateStoreName = from.StateStoreName;
        to.OutboxStateKey = from.OutboxStateKey;
        to.OutboxDeadLetterStateKey = from.OutboxDeadLetterStateKey;
        to.OutboxMaxRetryCount = from.OutboxMaxRetryCount;
        to.OutboxBaseDelaySeconds = from.OutboxBaseDelaySeconds;
        to.IdempotencyStateKeyPrefix = from.IdempotencyStateKeyPrefix;
        to.IdempotencyTtlMinutes = from.IdempotencyTtlMinutes;
        to.OtlpEndpoint = from.OtlpEndpoint;
        to.TelemetryServiceName = from.TelemetryServiceName;
        to.TelemetryEnvironment = from.TelemetryEnvironment;
        foreach (var client in from.ServiceClients)
        {
            to.AddServiceClient(client.InterfaceType, options =>
            {
                options.LoadBalancingStrategy = client.Options.LoadBalancingStrategy;
                options.InvocationMaxRetries = client.Options.InvocationMaxRetries;
                options.InvocationTimeoutSeconds = client.Options.InvocationTimeoutSeconds;
                options.CircuitBreakerFailureThreshold = client.Options.CircuitBreakerFailureThreshold;
                options.CircuitBreakerOpenSeconds = client.Options.CircuitBreakerOpenSeconds;
            }, client.AppIds);
        }

        foreach (var store in from.StateStores)
        {
            to.AddStateStore(store.EntityType, store.StoreName);
        }
    }

    private static string SelectAppId(string[] appIds, LoadBalancingStrategy strategy, ref int cursor, IHttpContextAccessor? httpContextAccessor)
    {
        if (appIds.Length == 1)
        {
            return appIds[0];
        }

        return strategy switch
        {
            LoadBalancingStrategy.Random => appIds[Random.Shared.Next(0, appIds.Length)],
            LoadBalancingStrategy.Sticky => SelectSticky(appIds, httpContextAccessor),
            _ => appIds[Interlocked.Increment(ref cursor) % appIds.Length]
        };
    }

    private static string SelectSticky(string[] appIds, IHttpContextAccessor? httpContextAccessor)
    {
        var stickyKey = httpContextAccessor?.HttpContext?.TraceIdentifier;
        if (string.IsNullOrWhiteSpace(stickyKey))
        {
            return appIds[Random.Shared.Next(0, appIds.Length)];
        }

        var index = Math.Abs(stickyKey.GetHashCode(StringComparison.Ordinal)) % appIds.Length;
        return appIds[index];
    }
}
