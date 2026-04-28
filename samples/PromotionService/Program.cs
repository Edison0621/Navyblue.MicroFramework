using Dapr.Client;
using PromotionService.Repositories;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

var daprGrpcEndpoint = builder.Configuration["Dapr:GrpcEndpoint"] ?? "http://localhost:50001";
var daprHttpEndpoint = builder.Configuration["Dapr:HttpEndpoint"] ?? "http://localhost:3512";
builder.Services.AddDaprClient(clientBuilder =>
{
    clientBuilder.UseGrpcEndpoint(daprGrpcEndpoint);
    clientBuilder.UseHttpEndpoint(daprHttpEndpoint);
});
builder.Services.AddScoped<IPromotionRepository, DaprPromotionRepository>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("state operation", StringComparison.OrdinalIgnoreCase))
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("PromotionServiceUnavailable");
        logger.LogWarning(ex, "Promotion service dependency unavailable for {Path}", context.Request.Path);
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                data = (object?)null,
                error = new { code = "dependency_unavailable", message = "Promotion dependency is temporarily unavailable." },
                traceId = context.TraceIdentifier
            });
        }
    }
});

app.MapGet("/", () => Results.Ok(new { service = "PromotionService", status = "ok" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));
app.MapGet("/ops/dashboard/overview", () => Results.Ok(RuntimeSnapshot.Build()));
app.MapControllers();

app.Run();

internal static class RuntimeSnapshot
{
    private static readonly DateTimeOffset StartedAt = DateTimeOffset.UtcNow;

    public static object Build()
    {
        using var process = Process.GetCurrentProcess();
        var uptimeMs = Math.Max((DateTimeOffset.UtcNow - StartedAt).TotalMilliseconds, 1d);
        var cpuPercent = process.TotalProcessorTime.TotalMilliseconds / (Environment.ProcessorCount * uptimeMs) * 100d;
        return new
        {
            generatedAt = DateTimeOffset.UtcNow,
            runtime = new
            {
                cpuPercent = Math.Round(cpuPercent, 2),
                memoryMb = Math.Round(process.WorkingSet64 / 1024d / 1024d, 2),
                managedMemoryMb = Math.Round(GC.GetTotalMemory(false) / 1024d / 1024d, 2),
                threadCount = process.Threads.Count,
                handleCount = process.HandleCount,
                uptimeMinutes = Math.Round((DateTimeOffset.UtcNow - StartedAt).TotalMinutes, 1)
            }
        };
    }
}
