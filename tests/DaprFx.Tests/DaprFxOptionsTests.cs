using DaprFx.Core;

namespace DaprFx.Tests;

public class DaprFxOptionsTests
{
    [Fact]
    public void AddServiceClient_WithMultipleAppIds_RemovesDuplicatesAndStoresRegistration()
    {
        var options = new DaprFxOptions();

        options.AddServiceClient(typeof(IDemoService), "svc-a", "svc-b", "svc-a");

        var registration = Assert.Single(options.ServiceClients);
        Assert.Equal(typeof(IDemoService), registration.InterfaceType);
        Assert.Equal(["svc-a", "svc-b"], registration.AppIds);
    }

    [Fact]
    public void AddServiceClient_WithConfigureDelegate_AppliesClientOverrides()
    {
        var options = new DaprFxOptions();

        options.AddServiceClient(typeof(IDemoService), client =>
        {
            client.LoadBalancingStrategy = LoadBalancingStrategy.Sticky;
            client.InvocationTimeoutSeconds = 3;
        }, "svc-a", "svc-b");

        var registration = Assert.Single(options.ServiceClients);
        Assert.Equal(LoadBalancingStrategy.Sticky, registration.Options.LoadBalancingStrategy);
        Assert.Equal(3, registration.Options.InvocationTimeoutSeconds);
    }

    [Fact]
    public void AddStateStore_RegistersExpectedEntityAndStoreName()
    {
        var options = new DaprFxOptions();

        options.AddStateStore(typeof(DemoEntity), "statestore");

        var stateStore = Assert.Single(options.StateStores);
        Assert.Equal(typeof(DemoEntity), stateStore.EntityType);
        Assert.Equal("statestore", stateStore.StoreName);
    }

    private interface IDemoService;

    private sealed class DemoEntity;
}
