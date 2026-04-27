using DaprFx.Core;
using Microsoft.AspNetCore.Mvc;
using OrderService.Abstractions;
using OrderService.Models;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrderController(
    IEventBus eventBus,
    IStateStore<Order> stateStore,
    IProductService productService,
    IPromotionService promotionService,
    IInventoryService inventoryService,
    IAuditService auditService,
    ILogger<OrderController> logger) : ControllerBase
{
    private readonly IEventBus _eventBus = eventBus;
    private readonly IStateStore<Order> _stateStore = stateStore;
    private readonly IProductService _productService = productService;
    private readonly IPromotionService _promotionService = promotionService;
    private readonly IInventoryService _inventoryService = inventoryService;
    private readonly IAuditService _auditService = auditService;
    private readonly ILogger<OrderController> _logger = logger;

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidQuantity, "Quantity must be greater than zero.")));
        }

        var order = new Order
        {
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            PromoCode = request.PromoCode,
            Status = OrderStatus.Pending
        };

        try
        {
            var product = await _productService.GetProductAsync(order.ProductId);
            if (product is null)
            {
                order.Status = OrderStatus.Failed;
                order.FailureReason = "Product not found.";
                order.UpdatedAt = DateTimeOffset.UtcNow;
                await _stateStore.SaveAsync(order.Id, order, cancellationToken);
                return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Product not found.")));
            }

            order.OriginalAmount = Math.Round(product.Price * order.Quantity, 2);
            order.DiscountAmount = 0;
            order.FinalAmount = order.OriginalAmount;

            PromotionValidationResult? promotion = null;
            if (!string.IsNullOrWhiteSpace(order.PromoCode))
            {
                promotion = await _promotionService.ValidateAsync(new PromotionValidationRequest(order.PromoCode, order.OriginalAmount));
                if (promotion is null || !promotion.Valid)
                {
                    order.Status = OrderStatus.Failed;
                    order.FailureReason = $"Invalid promotion: {promotion?.Reason ?? "unknown"}";
                    order.UpdatedAt = DateTimeOffset.UtcNow;
                    await _stateStore.SaveAsync(order.Id, order, cancellationToken);
                    return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidPromotion, "Invalid promotion code.", new { reason = promotion?.Reason ?? "unknown" })));
                }

                order.DiscountAmount = promotion.DiscountAmount;
                order.FinalAmount = promotion.FinalAmount;
            }

            var reserved = await _inventoryService.ReserveAsync(order.ProductId, new InventoryQuantityRequest(order.Quantity));
            if (reserved is null)
            {
                order.Status = OrderStatus.Failed;
                order.FailureReason = "Inventory reservation failed.";
                order.UpdatedAt = DateTimeOffset.UtcNow;
                await _stateStore.SaveAsync(order.Id, order, cancellationToken);
                return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InventoryReservationFailed, "Inventory reservation failed.")));
            }

            order.Status = OrderStatus.Confirmed;
            order.FailureReason = null;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            await _stateStore.SaveAsync(order.Id, order, cancellationToken);
            await _eventBus.PublishAsync(
                new OrderCreatedEvent(order.Id, order.ProductId, order.Quantity, order.CreatedAt),
                topic: "order.created",
                cancellationToken: cancellationToken);

            return Ok(new ApiResponse<object>(true, new
            {
                Order = order,
                Product = product,
                Inventory = reserved,
                Promotion = promotion
            }, null));
        }
        catch (Exception ex)
        {
            order.Status = OrderStatus.Failed;
            order.FailureReason = ex.Message;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            _logger.LogWarning(ex, "Order creation failed, attempting inventory compensation. ProductId={ProductId}, Quantity={Quantity}",
                order.ProductId, order.Quantity);
            try
            {
                await _inventoryService.ReleaseAsync(order.ProductId, new InventoryQuantityRequest(order.Quantity));
            }
            catch (Exception compensationEx)
            {
                _logger.LogError(compensationEx, "Inventory compensation failed. ProductId={ProductId}, Quantity={Quantity}",
                    order.ProductId, order.Quantity);
                await TryPublishCompensationFailureAuditAsync(order, compensationEx, cancellationToken);
                await TryPublishCompensationFailureEventAsync(order, compensationEx, cancellationToken);
            }

            await _stateStore.SaveAsync(order.Id, order, cancellationToken);
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.OrderCreationFailed, "Order creation failed.", new { error = ex.Message })));
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderById(string id, CancellationToken cancellationToken)
    {
        var order = await _stateStore.GetAsync(id, cancellationToken);
        return order is null
            ? NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Order not found.")))
            : Ok(new ApiResponse<Order>(true, order, null));
    }

    private async Task TryPublishCompensationFailureAuditAsync(Order order, Exception compensationEx, CancellationToken cancellationToken)
    {
        try
        {
            await _auditService.PublishAuditAsync(new AuditEventRequest(
                ActorId: "system",
                Action: "order.compensation.failed",
                ResourceType: "order",
                ResourceId: order.Id,
                Result: "failed",
                Detail: compensationEx.Message));
        }
        catch (Exception auditEx)
        {
            _logger.LogError(auditEx, "Publishing compensation failure audit failed. OrderId={OrderId}", order.Id);
        }
    }

    private async Task TryPublishCompensationFailureEventAsync(Order order, Exception compensationEx, CancellationToken cancellationToken)
    {
        try
        {
            await _eventBus.PublishAsync(
                new OrderCompensationFailedEvent(
                    OrderId: order.Id,
                    ProductId: order.ProductId,
                    Quantity: order.Quantity,
                    Error: compensationEx.Message,
                    OccurredAt: DateTimeOffset.UtcNow),
                topic: "order.compensation.failed",
                cancellationToken: cancellationToken);
        }
        catch (Exception eventEx)
        {
            _logger.LogError(eventEx, "Publishing compensation failure event failed. OrderId={OrderId}", order.Id);
        }
    }
}
