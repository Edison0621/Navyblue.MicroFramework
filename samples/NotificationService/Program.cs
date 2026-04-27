using Dapr;
using Dapr.Client;
using NotificationService.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprClient();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddControllers().AddDapr();
builder.Services.AddScoped<INotificationRepository, DaprNotificationRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Ok(new { service = "NotificationService", status = "ok" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));
app.UseCloudEvents();
app.MapSubscribeHandler();
app.MapControllers();

app.Run();
