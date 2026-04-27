using DaprFx.Core;
using Microsoft.AspNetCore.Mvc;
using OrderService.Abstractions;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrderController(
    IEventBus eventBus,
    IConfiguration configuration,
    IStateStore<Order> stateStore,
    IStateStore<ShoppingCart> cartStateStore,
    IStateStore<UserOrderIndex> userOrderIndexStore,
    IStateStore<PaymentPendingIndex> paymentPendingIndexStore,
    IProductService productService,
    ICatalogService catalogService,
    IPromotionService promotionService,
    IInventoryService inventoryService,
    IAuditService auditService,
    ILogger<OrderController> logger) : ControllerBase
{
    private readonly IEventBus _eventBus = eventBus;
    private readonly IConfiguration _configuration = configuration;
    private readonly IStateStore<Order> _stateStore = stateStore;
    private readonly IStateStore<ShoppingCart> _cartStateStore = cartStateStore;
    private readonly IStateStore<UserOrderIndex> _userOrderIndexStore = userOrderIndexStore;
    private readonly IStateStore<PaymentPendingIndex> _paymentPendingIndexStore = paymentPendingIndexStore;
    private readonly IProductService _productService = productService;
    private readonly ICatalogService _catalogService = catalogService;
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

        var resolution = await ResolveLineAsync(request.ProductId, request.Quantity, cancellationToken);
        if (resolution.Error is not null)
        {
            return resolution.Error;
        }

        var order = new Order
        {
            UserId = string.IsNullOrWhiteSpace(request.UserId) ? null : request.UserId.Trim(),
            PromoCode = request.PromoCode,
            Status = OrderStatus.Pending,
            SubOrders = BuildSubOrders([(resolution.Line!, resolution.ShopId)])
        };

        return await FinalizeOrderAsync(order, resolution.Product, cancellationToken);
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> CheckoutFromCart([FromBody] CheckoutCartRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidUserId, "UserId is required.")));
        }

        var userId = request.UserId.Trim();
        var cart = await _cartStateStore.GetAsync(CartController.BuildCartStateKey(userId), cancellationToken);
        var merged = CartController.MergeLines(cart?.Lines ?? []);
        if (merged.Count == 0)
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.EmptyCart, "Cart is empty.")));
        }

        var pairs = new List<(OrderLine Line, string ShopId)>();
        foreach (var line in merged)
        {
            if (line.Quantity <= 0)
            {
                continue;
            }

            var resolution = await ResolveLineAsync(line.ProductId, line.Quantity, cancellationToken);
            if (resolution.Error is not null)
            {
                return resolution.Error;
            }

            pairs.Add((resolution.Line!, resolution.ShopId));
        }

        if (pairs.Count == 0)
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.EmptyCart, "Cart is empty.")));
        }

        var order = new Order
        {
            UserId = userId,
            PromoCode = request.PromoCode,
            Status = OrderStatus.Pending,
            SubOrders = BuildSubOrders(pairs)
        };

        var actionResult = await FinalizeOrderAsync(order, primaryProduct: null, cancellationToken);
        if (actionResult is OkObjectResult)
        {
            await _cartStateStore.DeleteAsync(CartController.BuildCartStateKey(userId), cancellationToken);
        }

        return actionResult;
    }

    [HttpGet("by-user/{userId}")]
    public async Task<IActionResult> ListOrdersByUser(string userId, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidUserId, "UserId is required.")));
        }

        var safeTake = Math.Clamp(take, 1, 100);
        var key = UserOrderIndex.StateKey(userId.Trim());
        var index = await _userOrderIndexStore.GetAsync(key, cancellationToken);
        if (index?.OrderIds is null || index.OrderIds.Count == 0)
        {
            return Ok(new ApiResponse<IReadOnlyList<Order>>(true, [], null));
        }

        var orders = new List<Order>();
        foreach (var orderId in index.OrderIds.Take(safeTake))
        {
            var o = await _stateStore.GetAsync(orderId, cancellationToken);
            if (o is not null)
            {
                orders.Add(o);
            }
        }

        return Ok(new ApiResponse<IReadOnlyList<Order>>(true, orders, null));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderById(string id, CancellationToken cancellationToken)
    {
        var order = await _stateStore.GetAsync(id, cancellationToken);
        return order is null
            ? NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Order not found.")))
            : Ok(new ApiResponse<Order>(true, order, null));
    }

    private async Task<LineResolution> ResolveLineAsync(string productId, int quantity, CancellationToken cancellationToken)
    {
        var catalog = await TryGetCatalogItemAsync(productId, cancellationToken);
        if (catalog is { IsActive: false })
        {
            var failed = new Order { Status = OrderStatus.Failed, FailureReason = "Catalog item inactive." };
            await _stateStore.SaveAsync(failed.Id, failed, cancellationToken);
            return new LineResolution(
                BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidRequest, "Catalog item is inactive."))));
        }

        if (catalog is not null)
        {
            var unitPrice = Math.Round(catalog.Price, 2);
            var shopId = string.IsNullOrWhiteSpace(catalog.ShopId) ? "shop-default" : catalog.ShopId.Trim();
            var line = new OrderLine
            {
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = unitPrice,
                LineTotal = Math.Round(unitPrice * quantity, 2),
                ShopId = shopId
            };
            return new LineResolution(
                null,
                line,
                new ProductDto { Id = productId, Name = catalog.Name, Price = unitPrice, InStock = true },
                shopId);
        }

        ProductDto? product;
        try
        {
            product = await _productService.GetProductAsync(productId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ProductService lookup failed for {ProductId}", productId);
            product = null;
        }

        if (product is null)
        {
            var failed = new Order { Status = OrderStatus.Failed, FailureReason = "Product not found." };
            await _stateStore.SaveAsync(failed.Id, failed, cancellationToken);
            return new LineResolution(
                NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Product not found."))));
        }

        var fallbackPrice = Math.Round(product.Price, 2);
        const string fallbackShop = "shop-default";
        var fallbackLine = new OrderLine
        {
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = fallbackPrice,
            LineTotal = Math.Round(fallbackPrice * quantity, 2),
            ShopId = fallbackShop
        };
        return new LineResolution(null, fallbackLine, product, fallbackShop);
    }

    private async Task<CatalogItemDto?> TryGetCatalogItemAsync(string productId, CancellationToken cancellationToken)
    {
        try
        {
            var api = await _catalogService.GetItemAsync(productId);
            if (api is { Success: true, Data: not null })
            {
                return api.Data;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Catalog lookup failed for {ProductId}", productId);
        }

        return null;
    }

    private static List<SubOrder> BuildSubOrders(IReadOnlyList<(OrderLine Line, string ShopId)> items)
    {
        return items
            .GroupBy(x => x.ShopId)
            .Select(g =>
            {
                var lines = g.Select(x => x.Line).ToList();
                return new SubOrder
                {
                    ShopId = g.Key,
                    Lines = lines,
                    Subtotal = Math.Round(lines.Sum(x => x.LineTotal), 2),
                    FulfillmentStatus = SubOrderFulfillmentStatus.PendingShipment
                };
            })
            .ToList();
    }

    private async Task<IActionResult> FinalizeOrderAsync(Order order, ProductDto? primaryProduct, CancellationToken cancellationToken)
    {
        order.OriginalAmount = Math.Round(order.SubOrders.SelectMany(s => s.Lines).Sum(l => l.LineTotal), 2);
        order.DiscountAmount = 0;
        order.FinalAmount = order.OriginalAmount;

        PromotionValidationResult? promotion = null;
        if (!string.IsNullOrWhiteSpace(order.PromoCode))
        {
            promotion = await _promotionService.ValidateAsync(new PromotionValidationRequest(order.PromoCode!, order.OriginalAmount));
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

        var reservations = order.SubOrders.SelectMany(s => s.Lines).Select(l => (l.ProductId, l.Quantity)).ToList();

        try
        {
            var reservedSoFar = new List<(string ProductId, int Quantity)>();
            foreach (var (productId, qty) in reservations)
            {
                var reserved = await _inventoryService.ReserveAsync(productId, new InventoryQuantityRequest(qty));
                if (reserved is null)
                {
                    foreach (var (rolledBackProductId, rolledBackQty) in reservedSoFar)
                    {
                        try
                        {
                            await _inventoryService.ReleaseAsync(rolledBackProductId, new InventoryQuantityRequest(rolledBackQty));
                        }
                        catch (Exception releaseEx)
                        {
                            _logger.LogError(releaseEx, "Rolling back reservation failed. ProductId={ProductId}, Quantity={Quantity}", rolledBackProductId, rolledBackQty);
                        }
                    }

                    order.Status = OrderStatus.Failed;
                    order.FailureReason = "Inventory reservation failed.";
                    order.UpdatedAt = DateTimeOffset.UtcNow;
                    await _stateStore.SaveAsync(order.Id, order, cancellationToken);
                    return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InventoryReservationFailed, "Inventory reservation failed.")));
                }

                reservedSoFar.Add((productId, qty));
            }

            var paymentMinutes = Math.Clamp(_configuration.GetValue<int>("Order:PaymentTimeoutMinutes", 30), 5, 24 * 60);
            var now = DateTimeOffset.UtcNow;
            order.Status = OrderStatus.AwaitingPayment;
            order.PaymentDueAt = now.AddMinutes(paymentMinutes);
            order.PaidAt = null;
            order.FailureReason = null;
            order.UpdatedAt = now;
            await _stateStore.SaveAsync(order.Id, order, cancellationToken);
            await PaymentPendingIndexHelper.AppendOrderIdAsync(_paymentPendingIndexStore, order.Id, cancellationToken);
            await TryAppendUserOrderIndexAsync(order, cancellationToken);

            var (pid, totalQty) = SummarizeForEvent(order);
            await _eventBus.PublishAsync(
                BuildOrderCreatedEvent(order, pid, totalQty),
                topic: "order.created",
                cancellationToken: cancellationToken);

            return Ok(new ApiResponse<object>(true, new
            {
                Order = order,
                Product = primaryProduct,
                Promotion = promotion
            }, null));
        }
        catch (Exception ex)
        {
            order.Status = OrderStatus.Failed;
            order.FailureReason = ex.Message;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            _logger.LogWarning(ex, "Order creation failed, attempting inventory compensation.");
            foreach (var (productId, qty) in reservations)
            {
                try
                {
                    await _inventoryService.ReleaseAsync(productId, new InventoryQuantityRequest(qty));
                }
                catch (Exception compensationEx)
                {
                    _logger.LogError(compensationEx, "Inventory compensation failed. ProductId={ProductId}, Quantity={Quantity}", productId, qty);
                    await TryPublishCompensationFailureAuditAsync(order, compensationEx, cancellationToken);
                    await TryPublishCompensationFailureEventAsync(order, productId, qty, compensationEx, cancellationToken);
                }
            }

            await _stateStore.SaveAsync(order.Id, order, cancellationToken);
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.OrderCreationFailed, "Order creation failed.", new { error = ex.Message })));
        }
    }

    private async Task TryAppendUserOrderIndexAsync(Order order, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(order.UserId))
        {
            return;
        }

        try
        {
            var key = UserOrderIndex.StateKey(order.UserId!);
            var index = await _userOrderIndexStore.GetAsync(key, cancellationToken) ?? new UserOrderIndex();
            if (index.OrderIds.Contains(order.Id))
            {
                return;
            }

            index.OrderIds.Insert(0, order.Id);
            const int maxIds = 200;
            if (index.OrderIds.Count > maxIds)
            {
                index.OrderIds = index.OrderIds.Take(maxIds).ToList();
            }

            await _userOrderIndexStore.SaveAsync(key, index, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to append user order index for {UserId}", order.UserId);
        }
    }

    private static (string ProductId, int Quantity) SummarizeForEvent(Order order)
    {
        var lines = order.SubOrders.SelectMany(s => s.Lines).ToList();
        if (lines.Count == 0)
        {
            return ("", 0);
        }

        return (lines[0].ProductId, lines.Sum(l => l.Quantity));
    }

    private static OrderCreatedEvent BuildOrderCreatedEvent(Order order, string primaryProductId, int totalQuantity)
    {
        var subPayloads = order.SubOrders
            .Select(s => new OrderCreatedSubOrderPayload(
                s.ShopId,
                s.Id,
                s.Lines.Select(l => new OrderCreatedLinePayload(l.ProductId, l.Quantity, l.UnitPrice)).ToList()))
            .ToList();

        return new OrderCreatedEvent(
            order.Id,
            primaryProductId,
            totalQuantity,
            order.CreatedAt,
            order.UserId,
            subPayloads);
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

    private async Task TryPublishCompensationFailureEventAsync(
        Order order,
        string productId,
        int quantity,
        Exception compensationEx,
        CancellationToken cancellationToken)
    {
        try
        {
            await _eventBus.PublishAsync(
                new OrderCompensationFailedEvent(
                    OrderId: order.Id,
                    ProductId: productId,
                    Quantity: quantity,
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

    private sealed record LineResolution(
        IActionResult? Error,
        OrderLine? Line = null,
        ProductDto? Product = null,
        string ShopId = "shop-default");
}
