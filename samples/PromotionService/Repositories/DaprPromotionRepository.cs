using Dapr.Client;
using PromotionService.Models;

namespace PromotionService.Repositories;

public sealed class DaprPromotionRepository(DaprClient daprClient, ILogger<DaprPromotionRepository> logger) : IPromotionRepository
{
    private const string StateStoreName = "statestore";
    private const string PromotionsKey = "promotion:campaigns";

    public async Task<List<PromotionCampaign>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await ExecuteWithRetryAsync(
            ct => daprClient.GetStateAsync<List<PromotionCampaign>>(StateStoreName, PromotionsKey, cancellationToken: ct),
            "GetPromotions",
            cancellationToken) ?? [];
    }

    public Task SaveAllAsync(List<PromotionCampaign> campaigns, CancellationToken cancellationToken)
    {
        return ExecuteWithRetryAsync(
            ct => daprClient.SaveStateAsync(StateStoreName, PromotionsKey, campaigns, cancellationToken: ct),
            "SavePromotions",
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
                logger.LogWarning(ex, "Promotion state op {Operation} failed at attempt {Attempt}", operation, i + 1);
                if (i < RetryDelays.Length) await Task.Delay(RetryDelays[i], ct);
            }
        }

        throw new InvalidOperationException($"Promotion state operation {operation} failed after retries.", last);
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
