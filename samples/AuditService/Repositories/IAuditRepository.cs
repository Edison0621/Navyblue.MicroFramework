using AuditService.Models;

namespace AuditService.Repositories;

public interface IAuditRepository
{
    Task<List<AuditEvent>> GetAllAsync(CancellationToken cancellationToken);
    Task AppendAsync(AuditEvent entry, CancellationToken cancellationToken);
}
