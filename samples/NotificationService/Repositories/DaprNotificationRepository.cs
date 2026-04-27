using Dapr.Client;
using NotificationService.Models;

namespace NotificationService.Repositories;

public sealed class DaprNotificationRepository(DaprClient daprClient) : INotificationRepository
{
    private const string StateStoreName = "statestore";
    private const string NotificationsKey = "notifications:items";
    private const int MaxNotifications = 500;

    public async Task AppendAsync(NotificationItem item, CancellationToken cancellationToken)
    {
        var items = await daprClient.GetStateAsync<List<NotificationItem>>(StateStoreName, NotificationsKey, cancellationToken: cancellationToken) ?? [];
        items.Add(item);
        if (items.Count > MaxNotifications)
        {
            items = items.OrderByDescending(x => x.CreatedAt).Take(MaxNotifications).OrderBy(x => x.CreatedAt).ToList();
        }

        await daprClient.SaveStateAsync(StateStoreName, NotificationsKey, items, cancellationToken: cancellationToken);
    }

    public async Task<List<NotificationItem>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        var safeTake = take <= 0 ? 50 : Math.Min(take, 200);
        var items = await daprClient.GetStateAsync<List<NotificationItem>>(StateStoreName, NotificationsKey, cancellationToken: cancellationToken) ?? [];
        return items.OrderByDescending(x => x.CreatedAt).Take(safeTake).ToList();
    }
}
