using DaprFx.Core;
using DaprFx.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DaprFx.Tests;

public class HostingRegistrationTests
{
    [Fact]
    public void AddDaprMicroFramework_RegistersConfiguredOptions()
    {
        var services = new ServiceCollection();

        services.AddDaprMicroFramework(options =>
        {
            options.UseEventBus = true;
            options.PubSubName = "orderpubsub";
            options.ConfigStore = "appconfig";
            options.UseCryptography = true;
            options.OutboxDeadLetterStateKey = "order:outbox:dead";
            options.OutboxMaxRetryCount = 7;
            options.IdempotencyTtlMinutes = 30;
            options.TelemetryServiceName = "OrderService";
            options.TelemetryEnvironment = "Test";
            options.AddServiceClient(typeof(IDemoService), "svc-a", "svc-b");
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<DaprFxOptions>();

        Assert.True(options.UseEventBus);
        Assert.Equal("orderpubsub", options.PubSubName);
        Assert.Equal("appconfig", options.ConfigStore);
        Assert.True(options.UseCryptography);
        Assert.Equal("order:outbox:dead", options.OutboxDeadLetterStateKey);
        Assert.Equal(7, options.OutboxMaxRetryCount);
        Assert.Equal(30, options.IdempotencyTtlMinutes);
        Assert.Equal("OrderService", options.TelemetryServiceName);
        Assert.Equal("Test", options.TelemetryEnvironment);
        Assert.Single(options.ServiceClients);
    }

    [Fact]
    public void DaprFxOptions_DefaultReliabilityValues_AreExpected()
    {
        var options = new DaprFxOptions();

        Assert.Equal("daprfx:outbox", options.OutboxStateKey);
        Assert.Equal("daprfx:outbox:dead", options.OutboxDeadLetterStateKey);
        Assert.Equal(5, options.OutboxMaxRetryCount);
        Assert.Equal(2, options.OutboxBaseDelaySeconds);
        Assert.Equal(24 * 60, options.IdempotencyTtlMinutes);
    }

    [Fact]
    public void AddServiceClient_WithBlankAppIds_Throws()
    {
        var options = new DaprFxOptions();

        var ex = Assert.Throws<ArgumentException>(() =>
            options.AddServiceClient(typeof(IDemoService), "", " ", "\t"));

        Assert.Contains("appId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddDaprMicroFramework_WithEventBus_RegistersOutboxOperations()
    {
        var services = new ServiceCollection();

        services.AddDaprMicroFramework(options =>
        {
            options.UseEventBus = true;
            options.PubSubName = "orderpubsub";
        });

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IOutboxOperations));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IOutboxStore));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IDeadLetterStore));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddDaprMicroFramework_WithoutEventBus_DoesNotRegisterOutboxOperations()
    {
        var services = new ServiceCollection();

        services.AddDaprMicroFramework(options =>
        {
            options.UseEventBus = false;
            options.PubSubName = "orderpubsub";
        });

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IOutboxOperations));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IOutboxStore));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IDeadLetterStore));
    }

    private interface IDemoService;
}
