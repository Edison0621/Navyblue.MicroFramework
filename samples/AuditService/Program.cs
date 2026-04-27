using Dapr;
using AuditService.Repositories;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprClient();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddControllers().AddDapr();
builder.Services.AddScoped<IAuditRepository, DaprAuditRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Ok(new { service = "AuditService", status = "ok" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));
app.MapGet("/ops/dashboard/overview", () => Results.Ok(RuntimeSnapshot.Build()));
app.UseCloudEvents();
app.MapSubscribeHandler();
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
