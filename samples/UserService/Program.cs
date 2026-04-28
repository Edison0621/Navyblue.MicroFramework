using System.Text;
using System.Diagnostics;
using Dapr.Client;
using UserService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using UserService.Repositories;

var builder = WebApplication.CreateBuilder(args);

var daprGrpcEndpoint = builder.Configuration["Dapr:GrpcEndpoint"] ?? "http://localhost:50001";
var daprHttpEndpoint = builder.Configuration["Dapr:HttpEndpoint"] ?? "http://localhost:3505";
builder.Services.AddDaprClient(clientBuilder =>
{
    clientBuilder.UseGrpcEndpoint(daprGrpcEndpoint);
    clientBuilder.UseHttpEndpoint(daprHttpEndpoint);
});
builder.Services.AddControllers();
builder.Services.AddScoped<IUserRepository, DaprUserRepository>();
builder.Services.AddSingleton<IAddressBookRepository, DaprAddressBookRepository>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

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
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "UserService", status = "ok" }));
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
