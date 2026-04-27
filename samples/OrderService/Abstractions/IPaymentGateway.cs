namespace OrderService.Abstractions;

public interface IPaymentGateway
{
    Task<PaymentCaptureResult> CaptureAsync(string orderId, decimal amount, string? idempotencyKey, CancellationToken cancellationToken);
    Task<PaymentRefundResult> RefundAsync(string orderId, string? paymentTransactionId, decimal amount, string reason, CancellationToken cancellationToken);
}

public sealed record PaymentCaptureResult(
    bool Success,
    string? TransactionId,
    string? Error = null,
    string? ErrorCode = null,
    bool Retryable = false,
    string Gateway = "simulated",
    string? GatewayTransactionId = null);

public sealed record PaymentRefundResult(
    bool Success,
    string? RefundTransactionId,
    string? Error = null,
    string? ErrorCode = null,
    bool Retryable = false,
    string Gateway = "simulated",
    string? GatewayTransactionId = null);
