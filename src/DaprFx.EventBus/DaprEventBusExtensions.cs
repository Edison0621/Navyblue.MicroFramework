using Dapr;
using DaprFx.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using System.Text.Json;

namespace DaprFx.EventBus;

public static class DaprEventBusExtensions
{
    public static IServiceCollection AddDaprEventBus(this IServiceCollection services, string pubsubName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pubsubName);
        var subscriberItems = DiscoverSubscribers();
        foreach (var subscriber in subscriberItems)
        {
            services.TryAddTransient(subscriber.SubscriberType);
        }

        services.TryAddSingleton<IOutboxStore, DaprStateOutboxStore>();
        services.TryAddSingleton<IDeadLetterStore, DaprStateDeadLetterStore>();
        services.TryAddSingleton<IIdempotencyStore, DaprStateIdempotencyStore>();
        services.TryAddSingleton<IOutboxOperations, OutboxOperations>();
        services.TryAddSingleton(sp => new EventSubscriberRegistry(
            pubsubName,
            subscriberItems,
            sp.GetRequiredService<DaprFxOptions>(),
            sp.GetRequiredService<IDeadLetterStore>()));
        services.TryAddSingleton<IEventBus, ReliableEventBus>();
        services.AddHostedService<OutboxPublisherHostedService>();
        return services;
    }

    public static IEndpointRouteBuilder MapDaprEventBusSubscriptions(this IEndpointRouteBuilder endpoints)
    {
        var registry = endpoints.ServiceProvider.GetService<EventSubscriberRegistry>();
        if (registry is null)
        {
            return endpoints;
        }
        endpoints.MapSubscribeHandler();

        foreach (var item in registry.Items)
        {
            endpoints.MapPost($"/events/{item.Topic}", async (HttpContext context) =>
                {
                    await using var scope = context.RequestServices.CreateAsyncScope();
                    var subscriber = scope.ServiceProvider.GetRequiredService(item.SubscriberType);
                    var payload = await JsonSerializer.DeserializeAsync(context.Request.Body, item.EventType, cancellationToken: context.RequestAborted);
                    if (payload is null)
                    {
                        return Results.BadRequest("Event payload is null.");
                    }

                    var idempotencyStore = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
                    var cloudEventId = context.Request.Headers["ce-id"].ToString();
                    var idempotencyKey = string.IsNullOrWhiteSpace(cloudEventId)
                        ? $"{item.Topic}:{payload.GetHashCode():X}"
                        : $"{item.Topic}:{cloudEventId}";
                    if (!await idempotencyStore.TryBeginAsync(idempotencyKey, context.RequestAborted))
                    {
                        return Results.Ok();
                    }

                    var handleMethod = item.SubscriberType.GetMethod("HandleAsync", [item.EventType, typeof(CancellationToken)])!;
                    var task = (Task)handleMethod.Invoke(subscriber, [payload, context.RequestAborted])!;
                    await task;
                    await idempotencyStore.CompleteAsync(idempotencyKey, context.RequestAborted);
                    return Results.Ok();
                })
                .WithTopic(registry.PubSubName, item.Topic);
        }

        return endpoints;
    }

    private static IReadOnlyList<EventSubscriberItem> DiscoverSubscribers()
    {
        var result = new List<EventSubscriberItem>();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).ToArray();
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
            {
                var subscribers = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventSubscriber<>));
                foreach (var subscriber in subscribers)
                {
                    var eventType = subscriber.GetGenericArguments()[0];
                    var topic = type.GetCustomAttribute<DaprTopicAttribute>()?.Topic ?? eventType.Name;
                    result.Add(new EventSubscriberItem(type, eventType, topic));
                }
            }
        }

        return result;
    }
}
