using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using DaprFx.EventBus;

namespace DaprFx.Hosting;

public static class DaprFxWebApplicationExtensions
{
    public static WebApplication UseDaprFx(this WebApplication app)
    {
        app.MapControllers();
        app.MapDaprEventBusSubscriptions();
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Name == "self"
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = _ => true
        });
        return app;
    }
}
