using Dapr.Client;
using CatalogService.Repositories;
using System.Text;
using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprClient();
builder.Services.AddControllers();
builder.Services.AddScoped<ICatalogRepository, DaprCatalogRepository>();
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddSingleton(jwtOptions);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});
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
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("CatalogUnhandled");
        logger.LogError(ex, "Unhandled catalog exception for {Path}", context.Request.Path);
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                data = (object?)null,
                error = new { code = "internal_error", message = "Catalog service internal error." },
                traceId = context.TraceIdentifier
            });
        }
    }
});

app.MapGet("/", () => Results.Ok(new { service = "CatalogService", status = "ok" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));
app.MapGet("/ops/dashboard/overview", () => Results.Ok(RuntimeSnapshot.Build()));
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

internal sealed class JwtOptions
{
    public string Issuer { get; init; } = "DaprFx.AuthService";
    public string Audience { get; init; } = "DaprFx.Services";
    public string SigningKey { get; init; } = "DaprFx.Dev.Secret.Key.ChangeMe.2026!";
}

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
