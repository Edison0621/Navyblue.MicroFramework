using Dapr.Client;
using UserService.Models;

namespace UserService.Repositories;

public sealed class DaprUserRepository(DaprClient daprClient) : IUserRepository
{
    private const string StateStoreName = "statestore";
    private const string UserIndexStateKey = "users:index";

    public async Task<HashSet<Guid>> GetIdsAsync(CancellationToken cancellationToken)
    {
        return await daprClient.GetStateAsync<HashSet<Guid>>(StateStoreName, UserIndexStateKey, cancellationToken: cancellationToken) ?? [];
    }

    public Task SaveIdsAsync(HashSet<Guid> ids, CancellationToken cancellationToken)
    {
        return daprClient.SaveStateAsync(StateStoreName, UserIndexStateKey, ids, cancellationToken: cancellationToken);
    }

    public async Task<UserRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return await daprClient.GetStateAsync<UserRecord>(StateStoreName, BuildUserKey(id), cancellationToken: cancellationToken);
    }

    public Task SaveAsync(UserRecord user, CancellationToken cancellationToken)
    {
        return daprClient.SaveStateAsync(StateStoreName, BuildUserKey(user.Id), user, cancellationToken: cancellationToken);
    }

    private static string BuildUserKey(Guid id) => $"user:{id:N}";
}
