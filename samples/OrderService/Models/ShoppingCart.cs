namespace OrderService.Models;

public sealed class ShoppingCart
{
    public string UserId { get; set; } = string.Empty;
    public List<CartLine> Lines { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CartLine
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public sealed class ReplaceCartRequest
{
    public List<CartLineDto> Lines { get; set; } = [];
}

public sealed class CartLineDto
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public sealed class CheckoutCartRequest
{
    public string? PromoCode { get; set; }
    public Guid? AddressId { get; set; }
}
