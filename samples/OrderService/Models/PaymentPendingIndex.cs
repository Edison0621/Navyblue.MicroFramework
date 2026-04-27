namespace OrderService.Models;

/// <summary>
/// Global list of order ids currently awaiting payment (demo: single state blob).
/// </summary>
public sealed class PaymentPendingIndex
{
    public List<string> OrderIds { get; set; } = [];

    public const string StateKey = "order:payment-pending:index";
}
