using Dapr.Client;
using DaprFx.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DaprFx.EventBus;

internal sealed class OutboxPublisherHostedService(
    IOutboxStore outboxStore,
    DaprClient daprClient,
    EventSubscriberRegistry eventSubscriberRegistry,
    ILogger<OutboxPublisherHostedService> logger) : BackgroundService
{
    private readonly IOutboxStore _outboxStore = outboxStore;
    private readonly DaprClient _daprClient = daprClient;
    private readonly EventSubscriberRegistry _eventSubscriberRegistry = eventSubscriberRegistry;
    private readonly ILogger<OutboxPublisherHostedService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<OutboxMessage> batch;
            try
            {
                batch = await _outboxStore.DequeueBatchAsync(20, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Outbox dequeue failed; Dapr sidecar or state store may be unavailable. Retrying.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                continue;
            }

            if (batch.Count == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                continue;
            }

            foreach (var message in batch)
            {
                try
                {
                    await _daprClient.PublishEventAsync(_eventSubscriberRegistry.PubSubName, message.Topic, message.Payload, stoppingToken);
                }
                catch (Exception ex)
                {
                    var nextAttempt = message.AttemptCount + 1;
                    if (OutboxRetryPolicy.ShouldMoveToDeadLetter(nextAttempt, _eventSubscriberRegistry.Options.OutboxMaxRetryCount))
                    {
                        _logger.LogError(ex, "Outbox message {MessageId} moved to dead-letter after {RetryCount} retries.", message.Id, message.AttemptCount);
                        await _eventSubscriberRegistry.DeadLetterStore.AddAsync(
                            message with { AttemptCount = nextAttempt, LastError = ex.Message },
                            stoppingToken);
                        continue;
                    }

                    var delay = OutboxRetryPolicy.CalculateDelay(_eventSubscriberRegistry.Options.OutboxBaseDelaySeconds, nextAttempt);
                    _logger.LogWarning(ex, "Outbox publish failed for {Topic}, retry {Retry} in {DelaySeconds}s.", message.Topic, nextAttempt, delay.TotalSeconds);
                    await _outboxStore.EnqueueAsync(
                        message with
                        {
                            AttemptCount = nextAttempt,
                            LastError = ex.Message,
                            NextVisibleAt = DateTimeOffset.UtcNow.Add(delay)
                        },
                        stoppingToken);
                }
            }
        }
    }
}
