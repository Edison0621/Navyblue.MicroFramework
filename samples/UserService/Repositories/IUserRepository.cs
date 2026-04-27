using UserService.Models;

namespace UserService.Repositories;

public interface IUserRepository
{
    Task<HashSet<Guid>> GetIdsAsync(CancellationToken cancellationToken);
    Task SaveIdsAsync(HashSet<Guid> ids, CancellationToken cancellationToken);
    Task<UserRecord?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task SaveAsync(UserRecord user, CancellationToken cancellationToken);
}
