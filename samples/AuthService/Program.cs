using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Diagnostics;
using System.Text;
using Dapr.Client;
using AuthService.Models;
using AuthService.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var daprGrpcEndpoint = builder.Configuration["Dapr:GrpcEndpoint"] ?? "http://localhost:50001";
var daprHttpEndpoint = builder.Configuration["Dapr:HttpEndpoint"] ?? "http://localhost:3504";
builder.Services.AddDaprClient(clientBuilder =>
{
    clientBuilder.UseGrpcEndpoint(daprGrpcEndpoint);
    clientBuilder.UseHttpEndpoint(daprHttpEndpoint);
});
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddScoped<IRefreshTokenRepository, DaprRefreshTokenRepository>();

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

app.MapGet("/", () => Results.Ok(new { service = "AuthService", status = "ok" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));
app.MapGet("/ops/dashboard/overview", () => Results.Ok(RuntimeSnapshot.Build()));
app.MapControllers();

app.UseAuthentication();
app.UseAuthorization();

app.Run();

internal static class JwtTokenIssuer
{
    public static string Issue(AuthUserResponse user, JwtOptions options)
    {
        var username = string.IsNullOrWhiteSpace(user.Username) ? user.Email : user.Username;
        var safeUsername = string.IsNullOrWhiteSpace(username) ? user.Id.ToString() : username;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, safeUsername)
        };
        claims.AddRange((user.Roles ?? []).Where(role => !string.IsNullOrWhiteSpace(role)).Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(options.ExpiresMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
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

