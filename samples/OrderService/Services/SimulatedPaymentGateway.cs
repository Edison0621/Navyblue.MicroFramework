using OrderService.Abstractions;
using OrderService.Models;

namespace OrderService.Services;

public sealed class SimulatedPaymentGateway : IPaymentGateway
{
    private readonly PaymentGatewayOptions _options;

    public SimulatedPaymentGateway(PaymentGatewayOptions options)
    {
        _options = options;
    }

    public Task<PaymentCaptureResult> CaptureAsync(string orderId, decimal amount, string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (_options.SimulateTransientFailure)
        {
            return Task.FromResult(new PaymentCaptureResult(
                Success: false,
                TransactionId: null,
                Error: "Transient gateway timeout.",
                ErrorCode: "gateway_timeout",
                Retryable: true,
                Gateway: _options.Provider));
        }

        var suffix = string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString("N")[..8] : idempotencyKey.Trim();
        var tx = $"pay_{orderId[..Math.Min(8, orderId.Length)]}_{suffix}";
        return Task.FromResult(new PaymentCaptureResult(
            Success: true,
            TransactionId: tx,
            Error: null,
            Gateway: _options.Provider,
            GatewayTransactionId: tx));
    }

    public Task<PaymentRefundResult> RefundAsync(string orderId, string? paymentTransactionId, decimal amount, string reason, CancellationToken cancellationToken)
    {
        if (_options.SimulateTransientFailure)
        {
            return Task.FromResult(new PaymentRefundResult(
                Success: false,
                RefundTransactionId: null,
                Error: "Transient refund gateway failure.",
                ErrorCode: "gateway_timeout",
                Retryable: true,
                Gateway: _options.Provider));
        }

        var tx = $"refund_{orderId[..Math.Min(8, orderId.Length)]}_{Guid.NewGuid().ToString("N")[..8]}";
        return Task.FromResult(new PaymentRefundResult(
            Success: true,
            RefundTransactionId: tx,
            Error: null,
            Gateway: _options.Provider,
            GatewayTransactionId: paymentTransactionId));
    }
}
