using DaprFx.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Abstractions;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public sealed class OrderController(
    IEventBus eventBus,
    IConfiguration configuration,
    IStateStore<Order> stateStore,
    IStateStore<ShoppingCart> cartStateStore,
    IStateStore<UserOrderIndex> userOrderIndexStore,
    IStateStore<ShopOrderIndex> shopOrderIndexStore,
    IStateStore<PaymentPendingIndex> paymentPendingIndexStore,
    IProductService productService,
    ICatalogService catalogService,
    IPromotionService promotionService,
    IInventoryService inventoryService,
    IAuditService auditService,
    IUserService userService,
    ILogger<OrderController> logger) : ControllerBase
{
    private readonly IEventBus _eventBus = eventBus;
    private readonly IConfiguration _configuration = configuration;
    private readonly IStateStore<Order> _stateStore = stateStore;
    private readonly IStateStore<ShoppingCart> _cartStateStore = cartStateStore;
    private readonly IStateStore<UserOrderIndex> _userOrderIndexStore = userOrderIndexStore;
    private readonly IStateStore<ShopOrderIndex> _shopOrderIndexStore = shopOrderIndexStore;
    private readonly IStateStore<PaymentPendingIndex> _paymentPendingIndexStore = paymentPendingIndexStore;
    private readonly IProductService _productService = productService;
    private readonly ICatalogService _catalogService = catalogService;
    private readonly IPromotionService _promotionService = promotionService;
    private readonly IInventoryService _inventoryService = inventoryService;
    private readonly IAuditService _auditService = auditService;
    private readonly IUserService _userService = userService;
    private readonly ILogger<OrderController> _logger = logger;

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var callerUserId = OrderAccess.GetUserId(User);
        if (callerUserId is null)
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Missing user identity.")));
        }

        var userGuard = await UserStatusGuard.EnsureUserIsActiveAsync(this, _userService, callerUserId, cancellationToken);
        if (userGuard is not null)
        {
            return userGuard;
        }

        if (request.Quantity <= 0)
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidQuantity, "Quantity must be greater than zero.")));
        }

        var resolution = await ResolveLineAsync(request.ProductId, request.SkuId, request.Quantity, cancellationToken);
        if (resolution.Error is not null)
        {
            return resolution.Error;
        }

        var order = new Order
        {
            UserId = callerUserId,
            PromoCode = request.PromoCode,
            Status = OrderStatus.Pending,
            SubOrders = BuildSubOrders([(resolution.Line!, resolution.ShopId)])
        };

        return await FinalizeOrderAsync(order, resolution.Product, cancellationToken);
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> CheckoutFromCart([FromBody] CheckoutCartRequest request, CancellationToken cancellationToken)
    {
        var userId = OrderAccess.GetUserId(User);
        if (userId is null)
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Missing user identity.")));
        }

        var userGuard = await UserStatusGuard.EnsureUserIsActiveAsync(this, _userService, userId, cancellationToken);
        if (userGuard is not null)
        {
            return userGuard;
        }

        if (request.AddressId is null)
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidAddress, "AddressId is required for checkout.")));
        }

        ApiResponse<UserAddressSnapshotDto>? addrResp;
        try
        {
            addrResp = await _userService.GetUserAddressAsync(userId, request.AddressId.Value.ToString("D"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Address lookup via UserService failed for user {UserId}", userId);
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidAddress, "Could not load shipping address.")));
        }

        if (addrResp is not { Success: true, Data: not null })
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidAddress, "Address not found for this user.")));
        }

        var ship = addrResp.Data;
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

            var resolution = await ResolveLineAsync(line.ProductId, line.SkuId, line.Quantity, cancellationToken);
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
            SubOrders = BuildSubOrders(pairs),
            AddressId = request.AddressId,
            ShipToReceiverName = ship.ReceiverName,
            ShipToPhone = ship.Phone,
            ShipToRegion = ship.Region,
            ShipToDetail = ship.Detail
        };

        var actionResult = await FinalizeOrderAsync(order, primaryProduct: null, cancellationToken);
        if (actionResult is OkObjectResult)
        {
            await _cartStateStore.DeleteAsync(CartController.BuildCartStateKey(userId), cancellationToken);
        }

        return actionResult;
    }

    [HttpGet("me")]
    public async Task<IActionResult> ListMyOrders([FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        var userId = OrderAccess.GetUserId(User);
        if (userId is null)
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Missing user identity.")));
        }

        return await ListOrdersForUserAsync(userId, take, cancellationToken);
    }

    [HttpGet("me/search")]
    public async Task<IActionResult> SearchMyOrders(
        [FromQuery] string? status,
        [FromQuery] string? productId,
        [FromQuery] string? skuId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = OrderAccess.GetUserId(User);
        if (userId is null)
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Missing user identity.")));
        }

        return await SearchOrdersForUserAsync(userId, status, productId, skuId, from, to, page, pageSize, cancellationToken);
    }

    [HttpGet("by-user/{userId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ListOrdersByUser(string userId, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidUserId, "UserId is required.")));
        }

        return await ListOrdersForUserAsync(userId.Trim(), take, cancellationToken);
    }

    [HttpGet("by-user/{userId}/search")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SearchOrdersByUser(
        string userId,
        [FromQuery] string? status,
        [FromQuery] string? productId,
        [FromQuery] string? skuId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidUserId, "UserId is required.")));
        }

        return await SearchOrdersForUserAsync(userId.Trim(), status, productId, skuId, from, to, page, pageSize, cancellationToken);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderById(string id, CancellationToken cancellationToken)
    {
        var order = await _stateStore.GetAsync(id, cancellationToken);
        if (order is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Order not found.")));
        }

        if (!OrderAccess.CanAccessOrder(User, order))
        {
            return OrderAccess.Forbidden();
        }

        return Ok(new ApiResponse<Order>(true, order, null));
    }

    [HttpGet("by-shop/{shopId}")]
    public async Task<IActionResult> ListOrdersByShop(string shopId, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId))
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidRequest, "ShopId is required.")));
        }

        var normalizedShopId = shopId.Trim();
        if (!OrderAccess.CanAccessShop(User, normalizedShopId))
        {
            return OrderAccess.Forbidden();
        }

        return await ListOrdersForShopAsync(normalizedShopId, take, cancellationToken);
    }

    [HttpGet("by-shop/{shopId}/search")]
    public async Task<IActionResult> SearchOrdersByShop(
        string shopId,
        [FromQuery] string? orderStatus,
        [FromQuery] string? subOrderStatus,
        [FromQuery] string? productId,
        [FromQuery] string? skuId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId))
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidRequest, "ShopId is required.")));
        }

        var normalizedShopId = shopId.Trim();
        if (!OrderAccess.CanAccessShop(User, normalizedShopId))
        {
            return OrderAccess.Forbidden();
        }

        return await SearchOrdersForShopAsync(
            normalizedShopId,
            orderStatus,
            subOrderStatus,
            productId,
            skuId,
            from,
            to,
            page,
            pageSize,
            cancellationToken);
    }

    private async Task<IActionResult> ListOrdersForUserAsync(string userId, int take, CancellationToken cancellationToken)
    {
        var safeTake = Math.Clamp(take, 1, 100);
        var key = UserOrderIndex.StateKey(userId);
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

    private async Task<IActionResult> SearchOrdersForUserAsync(
        string userId,
        string? status,
        string? productId,
        string? skuId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var key = UserOrderIndex.StateKey(userId);
        var index = await _userOrderIndexStore.GetAsync(key, cancellationToken);
        if (index?.OrderIds is null || index.OrderIds.Count == 0)
        {
            var empty = new PagedResult<Order>([], 1, 20, 0);
            return Ok(new ApiResponse<PagedResult<Order>>(true, empty, null));
        }

        var all = new List<Order>();
        foreach (var orderId in index.OrderIds)
        {
            var o = await _stateStore.GetAsync(orderId, cancellationToken);
            if (o is not null)
            {
                all.Add(o);
            }
        }

        IEnumerable<Order> filtered = all;
        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(o => string.Equals(o.Status, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(productId))
        {
            var pid = productId.Trim();
            filtered = filtered.Where(o => o.SubOrders.SelectMany(s => s.Lines).Any(l => string.Equals(l.ProductId, pid, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(skuId))
        {
            var sid = skuId.Trim();
            filtered = filtered.Where(o => o.SubOrders.SelectMany(s => s.Lines).Any(l => string.Equals(l.SkuId, sid, StringComparison.OrdinalIgnoreCase)));
        }

        if (from.HasValue)
        {
            filtered = filtered.Where(o => o.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            filtered = filtered.Where(o => o.CreatedAt <= to.Value);
        }

        var q = new PageQuery(page, pageSize);
        var ordered = filtered.OrderByDescending(x => x.CreatedAt).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((q.SafePage - 1) * q.SafePageSize).Take(q.SafePageSize).ToList();
        var data = new PagedResult<Order>(items, q.SafePage, q.SafePageSize, total);
        return Ok(new ApiResponse<PagedResult<Order>>(true, data, null));
    }

    private async Task<LineResolution> ResolveLineAsync(string productId, string? skuId, int quantity, CancellationToken cancellationToken)
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
            var auditStatus = string.IsNullOrWhiteSpace(catalog.AuditStatus) && catalog.IsActive
                ? "Approved"
                : catalog.AuditStatus;
            if (!string.Equals(auditStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                return new LineResolution(
                    BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidRequest, "Catalog item is not approved."))));
            }

            var effectiveOnShelf = catalog.IsOnShelf || (string.IsNullOrWhiteSpace(catalog.AuditStatus) && catalog.IsActive);
            if (!effectiveOnShelf)
            {
                return new LineResolution(
                    BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidRequest, "Catalog item is off shelf."))));
            }

            var effectiveCategoryEnabled = catalog.CategoryEnabled || string.IsNullOrWhiteSpace(catalog.CategoryId);
            if (!effectiveCategoryEnabled)
            {
                return new LineResolution(
                    BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidRequest, "Catalog item's category is disabled."))));
            }
        }

        if (catalog is not null)
        {
            var normalizedSkuId = string.IsNullOrWhiteSpace(skuId) ? null : skuId.Trim();
            CatalogSkuDto? matchedSku = null;
            if (!string.IsNullOrWhiteSpace(normalizedSkuId))
            {
                matchedSku = catalog.Skus.FirstOrDefault(x => string.Equals(x.SkuId, normalizedSkuId, StringComparison.OrdinalIgnoreCase));
                if (matchedSku is null || !matchedSku.IsActive)
                {
                    return new LineResolution(
                        BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidRequest, "Sku is invalid or inactive."))));
                }
            }

            var unitPrice = Math.Round(matchedSku?.Price ?? catalog.Price, 2);
            var shopId = string.IsNullOrWhiteSpace(catalog.ShopId) ? "shop-default" : catalog.ShopId.Trim();
            var line = new OrderLine
            {
                ProductId = productId,
                SkuId = normalizedSkuId,
                SkuName = matchedSku?.Name,
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
            SkuId = null,
            SkuName = null,
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

        var reservations = order.SubOrders
            .SelectMany(s => s.Lines)
            .Select(l => (InventoryProductId: BuildInventoryProductId(l.ProductId, l.SkuId), l.Quantity))
            .ToList();

        try
        {
            var reservedSoFar = new List<(string InventoryProductId, int Quantity)>();
            foreach (var (inventoryProductId, qty) in reservations)
            {
                var reserved = await _inventoryService.ReserveAsync(inventoryProductId, new InventoryQuantityRequest(qty));
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
                            _logger.LogError(releaseEx, "Rolling back reservation failed. InventoryProductId={InventoryProductId}, Quantity={Quantity}", rolledBackProductId, rolledBackQty);
                        }
                    }

                    order.Status = OrderStatus.Failed;
                    order.FailureReason = "Inventory reservation failed.";
                    order.UpdatedAt = DateTimeOffset.UtcNow;
                    await _stateStore.SaveAsync(order.Id, order, cancellationToken);
                    return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InventoryReservationFailed, "Inventory reservation failed.")));
                }

                reservedSoFar.Add((inventoryProductId, qty));
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
            await TryAppendShopOrderIndexesAsync(order, cancellationToken);

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
            foreach (var (inventoryProductId, qty) in reservations)
            {
                try
                {
                    await _inventoryService.ReleaseAsync(inventoryProductId, new InventoryQuantityRequest(qty));
                }
                catch (Exception compensationEx)
                {
                    _logger.LogError(compensationEx, "Inventory compensation failed. InventoryProductId={InventoryProductId}, Quantity={Quantity}", inventoryProductId, qty);
                    await TryPublishCompensationFailureAuditAsync(order, compensationEx, cancellationToken);
                    await TryPublishCompensationFailureEventAsync(order, inventoryProductId, qty, compensationEx, cancellationToken);
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

    private async Task TryAppendShopOrderIndexesAsync(Order order, CancellationToken cancellationToken)
    {
        var shopIds = order.SubOrders
            .Select(x => x.ShopId?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var shopId in shopIds)
        {
            try
            {
                var key = ShopOrderIndex.StateKey(shopId!);
                var index = await _shopOrderIndexStore.GetAsync(key, cancellationToken) ?? new ShopOrderIndex();
                if (index.OrderIds.Contains(order.Id))
                {
                    continue;
                }

                index.OrderIds.Insert(0, order.Id);
                const int maxIds = 500;
                if (index.OrderIds.Count > maxIds)
                {
                    index.OrderIds = index.OrderIds.Take(maxIds).ToList();
                }

                await _shopOrderIndexStore.SaveAsync(key, index, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to append shop order index for {ShopId}", shopId);
            }
        }
    }

    private async Task<IActionResult> ListOrdersForShopAsync(string shopId, int take, CancellationToken cancellationToken)
    {
        var safeTake = Math.Clamp(take, 1, 100);
        var key = ShopOrderIndex.StateKey(shopId);
        var index = await _shopOrderIndexStore.GetAsync(key, cancellationToken);
        if (index?.OrderIds is null || index.OrderIds.Count == 0)
        {
            return Ok(new ApiResponse<IReadOnlyList<MerchantOrderView>>(true, [], null));
        }

        var items = new List<MerchantOrderView>();
        foreach (var orderId in index.OrderIds.Take(safeTake))
        {
            var o = await _stateStore.GetAsync(orderId, cancellationToken);
            if (o is null)
            {
                continue;
            }

            items.AddRange(ProjectMerchantOrderViews(o, shopId));
        }

        return Ok(new ApiResponse<IReadOnlyList<MerchantOrderView>>(true, items, null));
    }

    private async Task<IActionResult> SearchOrdersForShopAsync(
        string shopId,
        string? orderStatus,
        string? subOrderStatus,
        string? productId,
        string? skuId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var key = ShopOrderIndex.StateKey(shopId);
        var index = await _shopOrderIndexStore.GetAsync(key, cancellationToken);
        if (index?.OrderIds is null || index.OrderIds.Count == 0)
        {
            var empty = new PagedResult<MerchantOrderView>([], 1, 20, 0);
            return Ok(new ApiResponse<PagedResult<MerchantOrderView>>(true, empty, null));
        }

        var all = new List<MerchantOrderView>();
        foreach (var orderId in index.OrderIds)
        {
            var o = await _stateStore.GetAsync(orderId, cancellationToken);
            if (o is null)
            {
                continue;
            }

            all.AddRange(ProjectMerchantOrderViews(o, shopId));
        }

        IEnumerable<MerchantOrderView> filtered = all;
        if (!string.IsNullOrWhiteSpace(orderStatus))
        {
            filtered = filtered.Where(x => string.Equals(x.OrderStatus, orderStatus.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(subOrderStatus))
        {
            filtered = filtered.Where(x => string.Equals(x.SubOrderStatus, subOrderStatus.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(productId))
        {
            var pid = productId.Trim();
            filtered = filtered.Where(x => x.Lines.Any(l => string.Equals(l.ProductId, pid, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(skuId))
        {
            var sid = skuId.Trim();
            filtered = filtered.Where(x => x.Lines.Any(l => string.Equals(l.SkuId, sid, StringComparison.OrdinalIgnoreCase)));
        }

        if (from.HasValue)
        {
            filtered = filtered.Where(x => x.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            filtered = filtered.Where(x => x.CreatedAt <= to.Value);
        }

        var q = new PageQuery(page, pageSize);
        var ordered = filtered.OrderByDescending(x => x.CreatedAt).ToList();
        var total = ordered.Count;
        var pageItems = ordered.Skip((q.SafePage - 1) * q.SafePageSize).Take(q.SafePageSize).ToList();
        var data = new PagedResult<MerchantOrderView>(pageItems, q.SafePage, q.SafePageSize, total);
        return Ok(new ApiResponse<PagedResult<MerchantOrderView>>(true, data, null));
    }

    private static List<MerchantOrderView> ProjectMerchantOrderViews(Order order, string shopId)
    {
        return order.SubOrders
            .Where(x => string.Equals(x.ShopId, shopId, StringComparison.OrdinalIgnoreCase))
            .Select(x => new MerchantOrderView(
                OrderId: order.Id,
                ShopId: x.ShopId,
                UserId: order.UserId,
                OrderStatus: order.Status,
                CreatedAt: order.CreatedAt,
                UpdatedAt: order.UpdatedAt,
                ShopSubtotal: x.Subtotal,
                SubOrderId: x.Id,
                SubOrderStatus: x.FulfillmentStatus,
                Lines: x.Lines.ToList()))
            .ToList();
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

    private static string BuildInventoryProductId(string productId, string? skuId)
    {
        if (string.IsNullOrWhiteSpace(skuId))
        {
            return productId;
        }

        return $"{productId}::{skuId.Trim()}";
    }

    private static OrderCreatedEvent BuildOrderCreatedEvent(Order order, string primaryProductId, int totalQuantity)
    {
        var subPayloads = order.SubOrders
            .Select(s => new OrderCreatedSubOrderPayload(
                s.ShopId,
                s.Id,
                s.Lines.Select(l => new OrderCreatedLinePayload(l.ProductId, l.SkuId, l.Quantity, l.UnitPrice)).ToList()))
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
