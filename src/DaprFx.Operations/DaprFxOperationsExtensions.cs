using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DaprFx.Operations;

public static class DaprFxOperationsExtensions
{
    public static IServiceCollection AddDaprFxOperationsDashboard(this IServiceCollection services)
    {
        services.AddSingleton<OpsDashboardSnapshotBuilder>();
        services.AddSingleton<IOpsDashboardContributor, ProcessRuntimeDashboardContributor>();
        services.AddSingleton<IOpsDashboardContributor, OutboxDashboardContributor>();
        return services;
    }

    public static IServiceCollection AddOpsDashboardContributor<TContributor>(this IServiceCollection services)
        where TContributor : class, IOpsDashboardContributor
    {
        services.AddSingleton<IOpsDashboardContributor, TContributor>();
        return services;
    }

    public static IEndpointRouteBuilder MapDaprFxOperationsDashboard(this IEndpointRouteBuilder app, string pattern = "/ops/dashboard/overview")
    {
        app.MapGet(pattern, async (OpsDashboardSnapshotBuilder builder, CancellationToken cancellationToken) =>
        {
            var snapshot = await builder.BuildAsync(cancellationToken);
            return Results.Ok(snapshot);
        });
        return app;
    }
}
