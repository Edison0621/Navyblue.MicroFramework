using Dapr;
using AuditService.Repositories;

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
app.UseCloudEvents();
app.MapSubscribeHandler();
app.MapControllers();

app.Run();
