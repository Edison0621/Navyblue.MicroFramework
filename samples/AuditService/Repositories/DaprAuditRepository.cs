using AuditService.Models;
using Dapr.Client;

namespace AuditService.Repositories;

public sealed class DaprAuditRepository(DaprClient daprClient) : IAuditRepository
{
    private const string StateStoreName = "statestore";
    private const string AuditStateKey = "audit:events";
    private const int MaxAuditEvents = 500;

    public async Task<List<AuditEvent>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await daprClient.GetStateAsync<List<AuditEvent>>(StateStoreName, AuditStateKey, cancellationToken: cancellationToken) ?? [];
    }

    public async Task AppendAsync(AuditEvent entry, CancellationToken cancellationToken)
    {
        var items = await GetAllAsync(cancellationToken);
        items.Add(entry);
        if (items.Count > MaxAuditEvents)
        {
            items = items.OrderByDescending(x => x.CreatedAt).Take(MaxAuditEvents).OrderBy(x => x.CreatedAt).ToList();
        }

        await daprClient.SaveStateAsync(StateStoreName, AuditStateKey, items, cancellationToken: cancellationToken);
    }
}
