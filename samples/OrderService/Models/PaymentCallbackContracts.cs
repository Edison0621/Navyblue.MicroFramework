namespace OrderService.Models;

public sealed record PaymentCallbackRequest(
    string CallbackId,
    string OrderId,
    string TransactionId,
    decimal Amount,
    string Status,
    DateTimeOffset PaidAt);

public sealed class PaymentCallbackOptions
{
    public string SharedSecret { get; init; } = "DaprFx.Dev.Payment.Callback.Secret";
    public int AllowedClockSkewSeconds { get; init; } = 300;
}
