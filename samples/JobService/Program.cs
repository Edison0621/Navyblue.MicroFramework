using Dapr.Client;
using JobService.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprClient();
builder.Services.AddHttpClient("orderservice", (sp, client) =>
{
    var baseUrl = builder.Configuration["Jobs:OrderServiceBaseUrl"]?.Trim().TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        baseUrl = "http://localhost:5001";
    }

    client.BaseAddress = new Uri(baseUrl + "/", UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient("inventoryservice", (sp, client) =>
{
    var baseUrl = builder.Configuration["Jobs:InventoryServiceBaseUrl"]?.Trim().TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        baseUrl = "http://localhost:5004";
    }

    client.BaseAddress = new Uri(baseUrl + "/", UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient("catalogservice", (sp, client) =>
{
    var baseUrl = builder.Configuration["Jobs:CatalogServiceBaseUrl"]?.Trim().TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        baseUrl = "http://localhost:5008";
    }

    client.BaseAddress = new Uri(baseUrl + "/", UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(60);
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

app.MapGet("/", () => Results.Ok(new { service = "JobService", status = "ok" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));
app.MapControllers();

app.Run();
