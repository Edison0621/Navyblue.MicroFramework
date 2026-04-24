namespace DaprFx.Core;

public interface IEventBus
{
    Task PublishAsync<T>(T @event, string? topic = null, CancellationToken cancellationToken = default);
}
