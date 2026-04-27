using DaprFx.Core;
using OrderService.Models;

namespace OrderService.Services;

public static class PaymentPendingIndexHelper
{
    private const int MaxTrackedOrders = 500;

    public static async Task AppendOrderIdAsync(IStateStore<PaymentPendingIndex> store, string orderId, CancellationToken cancellationToken)
    {
        var index = await store.GetAsync(PaymentPendingIndex.StateKey, cancellationToken) ?? new PaymentPendingIndex();
        if (!index.OrderIds.Contains(orderId))
        {
            index.OrderIds.Insert(0, orderId);
            if (index.OrderIds.Count > MaxTrackedOrders)
            {
                index.OrderIds = index.OrderIds.Take(MaxTrackedOrders).ToList();
            }
        }

        await store.SaveAsync(PaymentPendingIndex.StateKey, index, cancellationToken);
    }

    public static async Task RemoveOrderIdAsync(IStateStore<PaymentPendingIndex> store, string orderId, CancellationToken cancellationToken)
    {
        var index = await store.GetAsync(PaymentPendingIndex.StateKey, cancellationToken);
        if (index?.OrderIds is null || index.OrderIds.Count == 0)
        {
            return;
        }

        if (!index.OrderIds.Remove(orderId))
        {
            return;
        }

        await store.SaveAsync(PaymentPendingIndex.StateKey, index, cancellationToken);
    }
}
