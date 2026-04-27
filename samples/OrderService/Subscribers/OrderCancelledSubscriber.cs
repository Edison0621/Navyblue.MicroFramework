using DaprFx.Core;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Subscribers;

[DaprTopic("order.cancelled")]
public sealed class OrderCancelledSubscriber(ILogger<OrderCancelledSubscriber> logger, EventAuditStore eventAuditStore) : IEventSubscriber<OrderCancelledEvent>
{
    private readonly ILogger<OrderCancelledSubscriber> _logger = logger;
    private readonly EventAuditStore _eventAuditStore = eventAuditStore;

    public Task HandleAsync(OrderCancelledEvent @event, CancellationToken cancellationToken = default)
    {
        var detail = $"fullOrder={@event.FullOrder};subOrderId={@event.SubOrderId ?? "-"}";
        _eventAuditStore.Add(new EventAuditItem(
            Topic: "order.cancelled",
            EventId: Guid.NewGuid().ToString("N"),
            OrderId: @event.OrderId,
            ProductId: "-",
            Quantity: 0,
            ReceivedAt: DateTimeOffset.UtcNow,
            Detail: detail));

        _logger.LogInformation("OrderCancelled event received: OrderId={OrderId}, FullOrder={FullOrder}", @event.OrderId, @event.FullOrder);
        return Task.CompletedTask;
    }
}
