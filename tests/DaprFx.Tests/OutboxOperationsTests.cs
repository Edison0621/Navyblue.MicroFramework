using DaprFx.Core;
using DaprFx.EventBus;

namespace DaprFx.Tests;

public class OutboxOperationsTests
{
    [Fact]
    public async Task ListDeadLettersAsync_AppliesTopicAndTimeFilters()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new FakeDeadLetterStore(
        [
            new OutboxMessage(Guid.NewGuid(), "topic.a", new { }, now.AddMinutes(-5)),
            new OutboxMessage(Guid.NewGuid(), "topic.b", new { }, now.AddMinutes(-2)),
            new OutboxMessage(Guid.NewGuid(), "topic.a", new { }, now.AddMinutes(-1))
        ]);
        var ops = new OutboxOperations(store);

        var result = await ops.ListDeadLettersAsync(
            take: 10,
            topic: "topic.a",
            from: now.AddMinutes(-3),
            to: now);

        Assert.Single(result);
        Assert.Equal("topic.a", result[0].Topic);
    }

    [Fact]
    public async Task ReplayAllDeadLettersAsync_DryRun_DoesNotRequeue()
    {
        List<OutboxMessage> messages =
        [
            new(Guid.NewGuid(), "topic.a", new { }, DateTimeOffset.UtcNow),
            new(Guid.NewGuid(), "topic.b", new { }, DateTimeOffset.UtcNow)
        ];
        var store = new FakeDeadLetterStore(messages);
        var ops = new OutboxOperations(store);

        var count = await ops.ReplayAllDeadLettersAsync(dryRun: true);

        Assert.Equal(2, count);
        Assert.Empty(store.RequeuedIds);
    }

    [Fact]
    public async Task ReplayAllDeadLettersAsync_RequeuesFilteredMessages()
    {
        var first = new OutboxMessage(Guid.NewGuid(), "topic.a", new { }, DateTimeOffset.UtcNow);
        var second = new OutboxMessage(Guid.NewGuid(), "topic.b", new { }, DateTimeOffset.UtcNow);
        var store = new FakeDeadLetterStore([first, second]);
        var ops = new OutboxOperations(store);

        var count = await ops.ReplayAllDeadLettersAsync(dryRun: false, topic: "topic.b");

        Assert.Equal(1, count);
        Assert.Single(store.RequeuedIds);
        Assert.Equal(second.Id, store.RequeuedIds[0]);
    }

    private sealed class FakeDeadLetterStore(IEnumerable<OutboxMessage> seed) : IDeadLetterStore
    {
        private readonly List<OutboxMessage> _messages = seed.ToList();
        public List<Guid> RequeuedIds { get; } = [];

        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            _messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OutboxMessage>> ListAsync(int take = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OutboxMessage>>(_messages.Take(take).ToList());

        public Task RequeueAsync(Guid messageId, CancellationToken cancellationToken = default)
        {
            RequeuedIds.Add(messageId);
            _messages.RemoveAll(x => x.Id == messageId);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid messageId, CancellationToken cancellationToken = default)
        {
            _messages.RemoveAll(x => x.Id == messageId);
            return Task.CompletedTask;
        }
    }
}
