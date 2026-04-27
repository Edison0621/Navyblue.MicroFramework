using Dapr.Client;

namespace AuthService.Repositories;

public sealed class DaprRefreshTokenRepository(DaprClient daprClient) : IRefreshTokenRepository
{
    private const string StateStoreName = "statestore";
    private const string RefreshTokenPrefix = "auth:refresh:";

    public Task SaveAsync(string refreshToken, Guid userId, CancellationToken cancellationToken)
    {
        return daprClient.SaveStateAsync(StateStoreName, $"{RefreshTokenPrefix}{refreshToken}", userId, cancellationToken: cancellationToken);
    }

    public Task<Guid?> GetUserIdAsync(string refreshToken, CancellationToken cancellationToken)
    {
        return daprClient.GetStateAsync<Guid?>(StateStoreName, $"{RefreshTokenPrefix}{refreshToken}", cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(string refreshToken, CancellationToken cancellationToken)
    {
        return daprClient.DeleteStateAsync(StateStoreName, $"{RefreshTokenPrefix}{refreshToken}", cancellationToken: cancellationToken);
    }
}
