using DaprFx.Core;

namespace DaprFx.EventBus;

internal sealed class EventSubscriberRegistry(
    string pubSubName,
    IReadOnlyList<EventSubscriberItem> items,
    DaprFxOptions options,
    IDeadLetterStore deadLetterStore)
{
    public string PubSubName { get; } = pubSubName;
    public IReadOnlyList<EventSubscriberItem> Items { get; } = items;
    public DaprFxOptions Options { get; } = options;
    public IDeadLetterStore DeadLetterStore { get; } = deadLetterStore;
}
