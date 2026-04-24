namespace DaprFx.EventBus;

internal sealed record EventSubscriberItem(Type SubscriberType, Type EventType, string Topic);
