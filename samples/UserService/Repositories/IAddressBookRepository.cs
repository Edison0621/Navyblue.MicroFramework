using UserService.Models;

namespace UserService.Repositories;

public interface IAddressBookRepository
{
    Task<IReadOnlyList<UserAddress>> ListAsync(Guid userId, CancellationToken cancellationToken);
    Task SaveAllAsync(Guid userId, IReadOnlyList<UserAddress> addresses, CancellationToken cancellationToken);
}
