using DaprFx.Core;
using OrderService.Models;

namespace OrderService.Abstractions;

public interface IAuditService
{
    [DaprInvoke("/api/audit/events")]
    Task PublishAuditAsync(AuditEventRequest request);
}
