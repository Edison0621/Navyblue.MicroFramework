using AuditService.Models;
using Dapr.Client;

namespace AuditService.Repositories;

public sealed class DaprAuditRepository(DaprClient daprClient, ILogger<DaprAuditRepository> logger) : IAuditRepository
{
    private const string StateStoreName = "statestore";
    private const string AuditStateKey = "audit:events";
    private const int MaxAuditEvents = 500;

    public async Task<List<AuditEvent>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await ExecuteWithRetryAsync(
            ct => daprClient.GetStateAsync<List<AuditEvent>>(StateStoreName, AuditStateKey, cancellationToken: ct),
            "GetAuditEvents",
            cancellationToken) ?? [];
    }

    public async Task AppendAsync(AuditEvent entry, CancellationToken cancellationToken)
    {
        var items = await GetAllAsync(cancellationToken);
        items.Add(entry);
        if (items.Count > MaxAuditEvents)
        {
            items = items.OrderByDescending(x => x.CreatedAt).Take(MaxAuditEvents).OrderBy(x => x.CreatedAt).ToList();
        }

        await ExecuteWithRetryAsync(
            ct => daprClient.SaveStateAsync(StateStoreName, AuditStateKey, items, cancellationToken: ct),
            "SaveAuditEvents",
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
                logger.LogWarning(ex, "Audit state op {Operation} failed at attempt {Attempt}", operation, i + 1);
                if (i < RetryDelays.Length) await Task.Delay(RetryDelays[i], ct);
            }
        }

        throw new InvalidOperationException($"Audit state operation {operation} failed after retries.", last);
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
