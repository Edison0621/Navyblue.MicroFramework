using System.Net.Sockets;
using Dapr.Client;
using UserService.Models;

namespace UserService.Repositories;

public sealed class DaprUserRepository(DaprClient daprClient, ILogger<DaprUserRepository> logger) : IUserRepository
{
    private const string StateStoreName = "statestore";
    private const string UserIndexStateKey = "users:index";
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(120),
        TimeSpan.FromMilliseconds(280),
        TimeSpan.FromMilliseconds(550)
    ];

    public async Task<HashSet<Guid>> GetIdsAsync(CancellationToken cancellationToken)
    {
        var ids = await ExecuteWithRetryAsync(
            ct => daprClient.GetStateAsync<HashSet<Guid>>(StateStoreName, UserIndexStateKey, cancellationToken: ct),
            "GetIds",
            cancellationToken);
        return ids ?? [];
    }

    public Task SaveIdsAsync(HashSet<Guid> ids, CancellationToken cancellationToken)
    {
        return ExecuteWithRetryAsync(
            ct => daprClient.SaveStateAsync(StateStoreName, UserIndexStateKey, ids, cancellationToken: ct),
            "SaveIds",
            cancellationToken);
    }

    public async Task<UserRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return await ExecuteWithRetryAsync(
            ct => daprClient.GetStateAsync<UserRecord>(StateStoreName, BuildUserKey(id), cancellationToken: ct),
            "GetUser",
            cancellationToken);
    }

    public Task SaveAsync(UserRecord user, CancellationToken cancellationToken)
    {
        return ExecuteWithRetryAsync(
            ct => daprClient.SaveStateAsync(StateStoreName, BuildUserKey(user.Id), user, cancellationToken: ct),
            "SaveUser",
            cancellationToken);
    }

    private static string BuildUserKey(Guid id) => $"user:{id:N}";

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                lastError = ex;
                logger.LogWarning(ex, "Dapr state op {OperationName} failed at attempt {Attempt}", operationName, attempt + 1);

                if (attempt < RetryDelays.Length)
                {
                    await Task.Delay(RetryDelays[attempt], cancellationToken);
                }
            }
        }

        throw new InvalidOperationException($"Dapr state operation {operationName} failed after retries.", lastError);
    }

    private async Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        await ExecuteWithRetryAsync<object?>(
            async ct =>
            {
                await operation(ct);
                return null;
            },
            operationName,
            cancellationToken);
    }

    private static bool IsTransient(Exception ex)
    {
        if (ex.GetType().FullName?.Contains("DaprException", StringComparison.Ordinal) == true)
        {
            return true;
        }

        if (ex is HttpRequestException or TimeoutException or SocketException)
        {
            return true;
        }

        return ex.InnerException is not null && IsTransient(ex.InnerException);
    }
}
