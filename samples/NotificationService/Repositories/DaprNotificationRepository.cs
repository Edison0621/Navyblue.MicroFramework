using Dapr.Client;
using NotificationService.Models;

namespace NotificationService.Repositories;

public sealed class DaprNotificationRepository(DaprClient daprClient, ILogger<DaprNotificationRepository> logger) : INotificationRepository
{
    private const string StateStoreName = "statestore";
    private const string NotificationsKey = "notifications:items";
    private const int MaxNotifications = 500;

    public async Task AppendAsync(NotificationItem item, CancellationToken cancellationToken)
    {
        var items = await GetItemsAsync(cancellationToken);
        items.Add(item);
        if (items.Count > MaxNotifications)
        {
            items = items.OrderByDescending(x => x.CreatedAt).Take(MaxNotifications).OrderBy(x => x.CreatedAt).ToList();
        }

        await SaveItemsAsync(items, cancellationToken);
    }

    public async Task<List<NotificationItem>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        var safeTake = take <= 0 ? 50 : Math.Min(take, 200);
        var items = await GetItemsAsync(cancellationToken);
        return items.OrderByDescending(x => x.CreatedAt).Take(safeTake).ToList();
    }

    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(260), TimeSpan.FromMilliseconds(500)];

    private async Task<List<NotificationItem>> GetItemsAsync(CancellationToken ct)
    {
        return await ExecuteWithRetryAsync(
            x => daprClient.GetStateAsync<List<NotificationItem>>(StateStoreName, NotificationsKey, cancellationToken: x),
            "GetNotifications",
            ct) ?? [];
    }

    private async Task SaveItemsAsync(List<NotificationItem> items, CancellationToken ct)
    {
        await ExecuteWithRetryAsync(
            x => daprClient.SaveStateAsync(StateStoreName, NotificationsKey, items, cancellationToken: x),
            "SaveNotifications",
            ct);
    }

    private async Task<TResult> ExecuteWithRetryAsync<TResult>(Func<CancellationToken, Task<TResult>> op, string operation, CancellationToken ct)
    {
        Exception? last = null;
        for (var i = 0; i <= RetryDelays.Length; i++)
        {
            try { return await op(ct); }
            catch (Exception ex) when (ex.GetType().FullName?.Contains("DaprException", StringComparison.Ordinal) == true || ex is HttpRequestException or TimeoutException)
            {
                last = ex;
                logger.LogWarning(ex, "Notification state op {Operation} failed at attempt {Attempt}", operation, i + 1);
                if (i < RetryDelays.Length) await Task.Delay(RetryDelays[i], ct);
            }
        }

        throw new InvalidOperationException($"Notification state operation {operation} failed after retries.", last);
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
