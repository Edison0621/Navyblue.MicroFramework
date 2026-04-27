using OrderService.Abstractions;

namespace OrderService.Services;

public sealed class SimulatedPaymentGateway : IPaymentGateway
{
    public Task<PaymentCaptureResult> CaptureAsync(string orderId, decimal amount, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var suffix = string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString("N")[..8] : idempotencyKey.Trim();
        var tx = $"pay_{orderId[..Math.Min(8, orderId.Length)]}_{suffix}";
        return Task.FromResult(new PaymentCaptureResult(true, tx, null));
    }

    public Task<PaymentRefundResult> RefundAsync(string orderId, string? paymentTransactionId, decimal amount, string reason, CancellationToken cancellationToken)
    {
        var tx = $"refund_{orderId[..Math.Min(8, orderId.Length)]}_{Guid.NewGuid().ToString("N")[..8]}";
        return Task.FromResult(new PaymentRefundResult(true, tx, null));
    }
}
