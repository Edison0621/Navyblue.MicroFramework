namespace DaprFx.Core;

public interface IEventSubscriber<T>
{
    Task HandleAsync(T @event, CancellationToken cancellationToken = default);
}
