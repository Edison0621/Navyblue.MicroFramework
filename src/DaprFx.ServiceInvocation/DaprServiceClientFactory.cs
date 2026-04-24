using System.Reflection;
using Dapr.Client;

namespace DaprFx.ServiceInvocation;

public static class DaprServiceClientFactory
{
    public static TService Create<TService>(DaprClient daprClient, string appId) where TService : class
        => Create<TService>(daprClient, appId, new InvocationPolicyOptions());

    public static TService Create<TService>(DaprClient daprClient, string appId, InvocationPolicyOptions invocationPolicyOptions) where TService : class
    {
        var proxy = DispatchProxy.Create<TService, DaprInvokeProxy>();
        ((DaprInvokeProxy)(object)proxy).Initialize(daprClient, appId, invocationPolicyOptions);
        return proxy;
    }
}
