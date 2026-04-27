namespace OrderService.Models;

public sealed class UserOrderIndex
{
    public List<string> OrderIds { get; set; } = [];

    internal static string StateKey(string userId) => $"userorders:{userId}";
}
