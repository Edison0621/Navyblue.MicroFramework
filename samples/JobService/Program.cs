using Dapr.Client;
using JobService.Repositories;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

var daprGrpcEndpoint = builder.Configuration["Dapr:GrpcEndpoint"] ?? "http://localhost:50001";
var daprHttpEndpoint = builder.Configuration["Dapr:HttpEndpoint"] ?? "http://localhost:3511";
builder.Services.AddDaprClient(clientBuilder =>
{
    clientBuilder.UseGrpcEndpoint(daprGrpcEndpoint);
    clientBuilder.UseHttpEndpoint(daprHttpEndpoint);
});
builder.Services.AddHttpClient("orderservice", (sp, client) =>
{
    var baseUrl = builder.Configuration["Jobs:OrderServiceBaseUrl"]?.Trim().TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        baseUrl = "http://localhost:5001";
    }

    client.BaseAddress = new Uri(baseUrl + "/", UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(12);
});
builder.Services.AddHttpClient("inventoryservice", (sp, client) =>
{
    var baseUrl = builder.Configuration["Jobs:InventoryServiceBaseUrl"]?.Trim().TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        baseUrl = "http://localhost:5004";
    }

    client.BaseAddress = new Uri(baseUrl + "/", UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(12);
});
builder.Services.AddHttpClient("catalogservice", (sp, client) =>
{
    var baseUrl = builder.Configuration["Jobs:CatalogServiceBaseUrl"]?.Trim().TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        baseUrl = "http://localhost:5008";
    }

    client.BaseAddress = new Uri(baseUrl + "/", UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(12);
});
builder.Services.AddControllers();
builder.Services.AddScoped<IJobRunRepository, DaprJobRunRepository>();
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
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JobServiceUnhandled");
        logger.LogError(ex, "Unhandled job service exception for {Path}", context.Request.Path);
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                data = (object?)null,
                error = new { code = "internal_error", message = "Job service internal error." },
                traceId = context.TraceIdentifier
            });
        }
    }
});

app.MapGet("/", () => Results.Ok(new { service = "JobService", status = "ok" }));
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
