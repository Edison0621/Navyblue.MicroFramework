using DaprFx.Core;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Subscribers;

[DaprTopic("order.created")]
public sealed class OrderCreatedSubscriber(
    ILogger<OrderCreatedSubscriber> logger,
    IConfiguration configuration,
    EventAuditStore eventAuditStore) : IEventSubscriber<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedSubscriber> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private readonly EventAuditStore _eventAuditStore = eventAuditStore;

    public Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        if (_configuration.GetValue<bool>("Demo:FailOrderSubscriber"))
        {
            throw new InvalidOperationException("Demo failure switch is enabled for OrderCreatedSubscriber.");
        }

        var detail = @event.SubOrders is { Count: > 0 } subs
            ? string.Join(" | ", subs.Select(s => $"{s.ShopId}:{s.SubOrderId}"))
            : null;
        _eventAuditStore.Add(new EventAuditItem(
            Topic: "order.created",
            EventId: Guid.NewGuid().ToString("N"),
            OrderId: @event.OrderId,
            ProductId: @event.ProductId,
            Quantity: @event.Quantity,
            ReceivedAt: DateTimeOffset.UtcNow,
            Detail: detail));

        _logger.LogInformation("OrderCreated event received: OrderId={OrderId}, ProductId={ProductId}, Quantity={Quantity}, Detail={Detail}",
            @event.OrderId, @event.ProductId, @event.Quantity, detail);
        return Task.CompletedTask;
    }
}
