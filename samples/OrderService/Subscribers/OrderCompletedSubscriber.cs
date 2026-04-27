using DaprFx.Core;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Subscribers;

[DaprTopic("order.completed")]
public sealed class OrderCompletedSubscriber(ILogger<OrderCompletedSubscriber> logger, EventAuditStore eventAuditStore) : IEventSubscriber<OrderCompletedEvent>
{
    private readonly ILogger<OrderCompletedSubscriber> _logger = logger;
    private readonly EventAuditStore _eventAuditStore = eventAuditStore;

    public Task HandleAsync(OrderCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        _eventAuditStore.Add(new EventAuditItem(
            Topic: "order.completed",
            EventId: Guid.NewGuid().ToString("N"),
            OrderId: @event.OrderId,
            ProductId: "-",
            Quantity: 0,
            ReceivedAt: DateTimeOffset.UtcNow,
            Detail: null));

        _logger.LogInformation("OrderCompleted event received: OrderId={OrderId}", @event.OrderId);
        return Task.CompletedTask;
    }
}
