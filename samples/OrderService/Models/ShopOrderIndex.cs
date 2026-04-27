namespace OrderService.Models;

public sealed class ShopOrderIndex
{
    public List<string> OrderIds { get; set; } = [];

    public static string StateKey(string shopId) => $"order:index:shop:{shopId}";
}
