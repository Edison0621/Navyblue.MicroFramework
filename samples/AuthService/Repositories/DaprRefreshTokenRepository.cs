using Dapr.Client;

namespace AuthService.Repositories;

public sealed class DaprRefreshTokenRepository(DaprClient daprClient, ILogger<DaprRefreshTokenRepository> logger) : IRefreshTokenRepository
{
    private const string StateStoreName = "statestore";
    private const string RefreshTokenPrefix = "auth:refresh:";

    public Task SaveAsync(string refreshToken, Guid userId, CancellationToken cancellationToken)
    {
        return ExecuteWithRetryAsync(
            ct => daprClient.SaveStateAsync(StateStoreName, $"{RefreshTokenPrefix}{refreshToken}", userId, cancellationToken: ct),
            "SaveRefreshToken",
            cancellationToken);
    }

    public Task<Guid?> GetUserIdAsync(string refreshToken, CancellationToken cancellationToken)
    {
        return ExecuteWithRetryAsync(
            ct => daprClient.GetStateAsync<Guid?>(StateStoreName, $"{RefreshTokenPrefix}{refreshToken}", cancellationToken: ct),
            "GetRefreshToken",
            cancellationToken);
    }

    public Task DeleteAsync(string refreshToken, CancellationToken cancellationToken)
    {
        return ExecuteWithRetryAsync(
            ct => daprClient.DeleteStateAsync(StateStoreName, $"{RefreshTokenPrefix}{refreshToken}", cancellationToken: ct),
            "DeleteRefreshToken",
            cancellationToken);
    }

    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(260), TimeSpan.FromMilliseconds(500)];

    private async Task<TResult> ExecuteWithRetryAsync<TResult>(Func<CancellationToken, Task<TResult>> op, string operation, CancellationToken ct)
    {
        Exception? last = null;
        for (var i = 0; i <= RetryDelays.Length; i++)
        {
            try { return await op(ct); }
            catch (Exception ex) when (ex.GetType().FullName?.Contains("DaprException", StringComparison.Ordinal) == true || ex is HttpRequestException or TimeoutException)
            {
                last = ex;
                logger.LogWarning(ex, "Auth refresh token state op {Operation} failed at attempt {Attempt}", operation, i + 1);
                if (i < RetryDelays.Length) await Task.Delay(RetryDelays[i], ct);
            }
        }

        throw new InvalidOperationException($"Auth refresh token operation {operation} failed after retries.", last);
    }

    private async Task ExecuteWithRetryAsync(Func<CancellationToken, Task> op, string operation, CancellationToken ct)
    {
        await ExecuteWithRetryAsync<object?>(
            async x =>
            {
                await op(x);
                return null;
            },
            operation,
            ct);
    }
}
