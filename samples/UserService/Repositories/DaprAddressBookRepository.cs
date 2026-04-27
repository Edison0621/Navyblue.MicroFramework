using Dapr.Client;
using UserService.Models;

namespace UserService.Repositories;

public sealed class DaprAddressBookRepository(DaprClient daprClient) : IAddressBookRepository
{
    private const string StateStoreName = "statestore";

    public async Task<IReadOnlyList<UserAddress>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var list = await daprClient.GetStateAsync<List<UserAddress>>(StateStoreName, BuildKey(userId), cancellationToken: cancellationToken);
        return list ?? [];
    }

    public Task SaveAllAsync(Guid userId, IReadOnlyList<UserAddress> addresses, CancellationToken cancellationToken)
    {
        return daprClient.SaveStateAsync(StateStoreName, BuildKey(userId), addresses.ToList(), cancellationToken: cancellationToken);
    }

    private static string BuildKey(Guid userId) => $"user:addresses:{userId:N}";
}
