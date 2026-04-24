using Dapr.Client;
using DaprFx.Core;

namespace DaprFx.EventBus;

public sealed class EventBus(DaprClient client, string pubsubName) : IEventBus
{
    private readonly DaprClient _client = client;
    private readonly string _pubsubName = pubsubName;

    public Task PublishAsync<T>(T @event, string? topic = null, CancellationToken cancellationToken = default)
        => _client.PublishEventAsync(_pubsubName, topic ?? typeof(T).Name, @event, cancellationToken);
}
