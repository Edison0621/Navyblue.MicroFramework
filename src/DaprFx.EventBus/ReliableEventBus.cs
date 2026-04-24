using DaprFx.Core;

namespace DaprFx.EventBus;

internal sealed class ReliableEventBus(IOutboxStore outboxStore) : IEventBus
{
    private readonly IOutboxStore _outboxStore = outboxStore;

    public Task PublishAsync<T>(T @event, string? topic = null, CancellationToken cancellationToken = default)
    {
        var message = new OutboxMessage(Guid.NewGuid(), topic ?? typeof(T).Name, @event!, DateTimeOffset.UtcNow);
        return _outboxStore.EnqueueAsync(message, cancellationToken);
    }
}
