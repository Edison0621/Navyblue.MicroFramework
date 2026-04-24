namespace DaprFx.Core;

public sealed class DaprFxOptions
{
    public bool UseEventBus { get; set; }
    public string? PubSubName { get; set; }
    public string? ConfigStore { get; set; }
    public bool UseCryptography { get; set; }
    public int InvocationMaxRetries { get; set; } = 2;
    public int InvocationTimeoutSeconds { get; set; } = 10;
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public int CircuitBreakerOpenSeconds { get; set; } = 30;
    public string StateStoreName { get; set; } = "statestore";
    public string OutboxStateKey { get; set; } = "daprfx:outbox";
    public string OutboxDeadLetterStateKey { get; set; } = "daprfx:outbox:dead";
    public int OutboxMaxRetryCount { get; set; } = 5;
    public int OutboxBaseDelaySeconds { get; set; } = 2;
    public string IdempotencyStateKeyPrefix { get; set; } = "daprfx:idempotency:";
    public int IdempotencyTtlMinutes { get; set; } = 24 * 60;
    public string? OtlpEndpoint { get; set; }
    public string TelemetryServiceName { get; set; } = "DaprFx.Service";
    public string TelemetryEnvironment { get; set; } = "Development";
    public List<ServiceClientRegistration> ServiceClients { get; } = [];
    public List<(Type EntityType, string StoreName)> StateStores { get; } = [];

    public DaprFxOptions AddServiceClient(Type interfaceType, string appId)
    {
        ArgumentNullException.ThrowIfNull(interfaceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ServiceClients.Add(new ServiceClientRegistration(interfaceType, [appId], new ServiceClientOptions()));
        return this;
    }

    public DaprFxOptions AddServiceClient<TInterface>(string appId) where TInterface : class
        => AddServiceClient(typeof(TInterface), appId);

    public DaprFxOptions AddServiceClient(Type interfaceType, params string[] appIds)
    {
        ArgumentNullException.ThrowIfNull(interfaceType);
        ArgumentNullException.ThrowIfNull(appIds);
        var filtered = appIds.Where(static s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (filtered.Length == 0)
        {
            throw new ArgumentException("At least one valid appId is required.", nameof(appIds));
        }

        ServiceClients.Add(new ServiceClientRegistration(interfaceType, filtered, new ServiceClientOptions()));
        return this;
    }

    public DaprFxOptions AddServiceClient(Type interfaceType, Action<ServiceClientOptions> configureClient, params string[] appIds)
    {
        ArgumentNullException.ThrowIfNull(configureClient);
        AddServiceClient(interfaceType, appIds);
        var last = ServiceClients[^1];
        configureClient(last.Options);
        return this;
    }

    public DaprFxOptions AddServiceClient<TInterface>(params string[] appIds) where TInterface : class
        => AddServiceClient(typeof(TInterface), appIds);

    public DaprFxOptions AddServiceClient<TInterface>(Action<ServiceClientOptions> configureClient, params string[] appIds) where TInterface : class
        => AddServiceClient(typeof(TInterface), configureClient, appIds);

    public DaprFxOptions AddStateStore(Type entityType, string storeName)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        StateStores.Add((entityType, storeName));
        return this;
    }

    public DaprFxOptions AddStateStore<TEntity>(string storeName) where TEntity : class
        => AddStateStore(typeof(TEntity), storeName);
}
