using System.Reflection;
using DaprFx.Core;
using DaprFx.Hosting;
using Microsoft.AspNetCore.Http;

namespace DaprFx.Tests;

public class LoadBalancingSelectionTests
{
    [Fact]
    public void RoundRobin_ReturnsValuesInOrder()
    {
        var appIds = new[] { "svc-a", "svc-b" };
        var cursor = -1;

        var first = InvokeSelectAppId(appIds, LoadBalancingStrategy.RoundRobin, ref cursor, null);
        var second = InvokeSelectAppId(appIds, LoadBalancingStrategy.RoundRobin, ref cursor, null);
        var third = InvokeSelectAppId(appIds, LoadBalancingStrategy.RoundRobin, ref cursor, null);

        Assert.Equal("svc-a", first);
        Assert.Equal("svc-b", second);
        Assert.Equal("svc-a", third);
    }

    [Fact]
    public void Sticky_WithSameTraceIdentifier_ReturnsSameAppId()
    {
        var appIds = new[] { "svc-a", "svc-b", "svc-c" };
        var cursor = -1;
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                TraceIdentifier = "trace-001"
            }
        };

        var first = InvokeSelectAppId(appIds, LoadBalancingStrategy.Sticky, ref cursor, accessor);
        var second = InvokeSelectAppId(appIds, LoadBalancingStrategy.Sticky, ref cursor, accessor);

        Assert.Equal(first, second);
        Assert.Contains(first, appIds);
    }

    private static string InvokeSelectAppId(string[] appIds, LoadBalancingStrategy strategy, ref int cursor, IHttpContextAccessor? accessor)
    {
        var method = typeof(DaprFxServiceCollectionExtensions).GetMethod(
            "SelectAppId",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var args = new object?[] { appIds, strategy, cursor, accessor };
        var result = (string)method.Invoke(null, args)!;
        cursor = (int)args[2]!;
        return result;
    }
}
