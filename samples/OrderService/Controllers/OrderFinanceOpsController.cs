using DaprFx.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Models;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders/ops")]
public sealed class OrderFinanceOpsController(
    IStateStore<Order> orderStore,
    IStateStore<RefundLedgerIndex> refundLedgerStore) : ControllerBase
{
    [HttpPost("reconcile-refunds")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ReconcileRefunds([FromQuery] int take = 200, CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, 1000);
        var ledger = await refundLedgerStore.GetAsync(RefundLedgerIndex.StateKey, cancellationToken) ?? new RefundLedgerIndex();
        var sample = ledger.Entries.Take(safeTake).ToList();

        var mismatches = new List<object>();
        foreach (var item in sample)
        {
            var order = await orderStore.GetAsync(item.OrderId, cancellationToken);
            if (order is null)
            {
                mismatches.Add(new { item.OrderId, item.AfterSaleId, issue = "order_missing" });
                continue;
            }

            var afterSale = order.AfterSales.FirstOrDefault(x => string.Equals(x.Id, item.AfterSaleId, StringComparison.Ordinal));
            if (afterSale is null)
            {
                mismatches.Add(new { item.OrderId, item.AfterSaleId, issue = "aftersale_missing" });
                continue;
            }

            if (!string.Equals(afterSale.RefundStatus, AfterSaleRefundStatus.Succeeded, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(afterSale.RefundTransactionId, item.RefundTransactionId, StringComparison.Ordinal)
                || (afterSale.RefundedAmount ?? 0) != item.Amount)
            {
                mismatches.Add(new
                {
                    item.OrderId,
                    item.AfterSaleId,
                    issue = "state_mismatch",
                    expected = item,
                    actual = new { afterSale.RefundStatus, afterSale.RefundTransactionId, afterSale.RefundedAmount }
                });
            }
        }

        return Ok(new ApiResponse<object>(true, new
        {
            checkedCount = sample.Count,
            mismatchCount = mismatches.Count,
            mismatches
        }, null));
    }
}
