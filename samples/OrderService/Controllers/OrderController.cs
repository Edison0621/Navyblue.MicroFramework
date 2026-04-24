using DaprFx.Core;
using Microsoft.AspNetCore.Mvc;
using OrderService.Abstractions;
using OrderService.Models;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrderController(IEventBus eventBus, IStateStore<Order> stateStore, IProductService productService) : ControllerBase
{
    private readonly IEventBus _eventBus = eventBus;
    private readonly IStateStore<Order> _stateStore = stateStore;
    private readonly IProductService _productService = productService;

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = new Order
        {
            ProductId = request.ProductId,
            Quantity = request.Quantity
        };

        await _stateStore.SaveAsync(order.Id, order, cancellationToken);
        var product = await _productService.GetProductAsync(order.ProductId);
        await _eventBus.PublishAsync(
            new OrderCreatedEvent(order.Id, order.ProductId, order.Quantity, order.CreatedAt),
            topic: "order.created",
            cancellationToken: cancellationToken);

        return Ok(new
        {
            Order = order,
            Product = product
        });
    }
}
