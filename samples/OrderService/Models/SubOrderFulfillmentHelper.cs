namespace OrderService.Models;

public static class SubOrderFulfillmentHelper
{
    public static void Normalize(SubOrder sub)
    {
        if (string.IsNullOrWhiteSpace(sub.FulfillmentStatus))
        {
            sub.FulfillmentStatus = SubOrderFulfillmentStatus.PendingShipment;
        }
    }

    public static bool AllActiveSubOrdersDelivered(IReadOnlyList<SubOrder> subOrders)
    {
        foreach (var s in subOrders)
        {
            Normalize(s);
        }

        var active = subOrders.Where(s => s.FulfillmentStatus != SubOrderFulfillmentStatus.Cancelled).ToList();
        return active.Count > 0 && active.All(s => s.FulfillmentStatus == SubOrderFulfillmentStatus.Delivered);
    }
}
