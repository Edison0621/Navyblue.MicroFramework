namespace OrderService.Models;

public sealed class RefundLedgerIndex
{
    public const string StateKey = "order:refund:ledger:index";
    public List<RefundLedgerEntry> Entries { get; set; } = [];
}

public sealed class RefundLedgerEntry
{
    public string OrderId { get; set; } = string.Empty;
    public string AfterSaleId { get; set; } = string.Empty;
    public string RefundTransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTimeOffset RefundedAt { get; set; }
}
