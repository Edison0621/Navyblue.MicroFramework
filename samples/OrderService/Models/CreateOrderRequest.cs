namespace OrderService.Models;

public sealed class CreateOrderRequest
{
    public string ProductId { get; set; } = string.Empty;
    public string? SkuId { get; set; }
    public int Quantity { get; set; }
    public string? PromoCode { get; set; }
}
