using NotificationService.Models;

namespace NotificationService.Repositories;

public interface INotificationRepository
{
    Task AppendAsync(NotificationItem item, CancellationToken cancellationToken);
    Task<List<NotificationItem>> GetRecentAsync(int take, CancellationToken cancellationToken);
}
