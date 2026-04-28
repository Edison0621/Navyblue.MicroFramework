using Dapr.Client;
using UserService.Models;

namespace UserService.Repositories;

public sealed class DaprAddressBookRepository(DaprClient daprClient, ILogger<DaprAddressBookRepository> logger) : IAddressBookRepository
{
    private const string StateStoreName = "statestore";

    public async Task<IReadOnlyList<UserAddress>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var list = await ExecuteWithRetryAsync(
            ct => daprClient.GetStateAsync<List<UserAddress>>(StateStoreName, BuildKey(userId), cancellationToken: ct),
            "GetAddressBook",
            cancellationToken);
        return list ?? [];
    }

    public Task SaveAllAsync(Guid userId, IReadOnlyList<UserAddress> addresses, CancellationToken cancellationToken)
    {
        return ExecuteWithRetryAsync(
            ct => daprClient.SaveStateAsync(StateStoreName, BuildKey(userId), addresses.ToList(), cancellationToken: ct),
            "SaveAddressBook",
            cancellationToken);
    }

    private static string BuildKey(Guid userId) => $"user:addresses:{userId:N}";

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
                logger.LogWarning(ex, "Address state op {Operation} failed at attempt {Attempt}", operation, i + 1);
                if (i < RetryDelays.Length) await Task.Delay(RetryDelays[i], ct);
            }
        }

        throw new InvalidOperationException($"Address state operation {operation} failed after retries.", last);
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
