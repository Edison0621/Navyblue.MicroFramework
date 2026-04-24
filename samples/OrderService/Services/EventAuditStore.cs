using System.Collections.Concurrent;

namespace OrderService.Services;

public sealed class EventAuditStore
{
    private readonly ConcurrentQueue<EventAuditItem> _items = new();

    public void Add(EventAuditItem item)
    {
        _items.Enqueue(item);
        while (_items.Count > 200 && _items.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<EventAuditItem> List(int take = 50)
        => _items.Reverse().Take(Math.Max(1, take)).ToList();
}
