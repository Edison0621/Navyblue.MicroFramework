using DaprFx.Core;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Subscribers;

[DaprTopic("order.paid")]
public sealed class OrderPaidSubscriber(ILogger<OrderPaidSubscriber> logger, EventAuditStore eventAuditStore) : IEventSubscriber<OrderPaidEvent>
{
    private readonly ILogger<OrderPaidSubscriber> _logger = logger;
    private readonly EventAuditStore _eventAuditStore = eventAuditStore;

    public Task HandleAsync(OrderPaidEvent @event, CancellationToken cancellationToken = default)
    {
        _eventAuditStore.Add(new EventAuditItem(
            Topic: "order.paid",
            EventId: Guid.NewGuid().ToString("N"),
            OrderId: @event.OrderId,
            ProductId: "-",
            Quantity: 0,
            ReceivedAt: DateTimeOffset.UtcNow,
            Detail: null));

        _logger.LogInformation("OrderPaid event received: OrderId={OrderId}", @event.OrderId);
        return Task.CompletedTask;
    }
}
