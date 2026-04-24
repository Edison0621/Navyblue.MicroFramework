using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/overview", async (IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
    var services = configuration
        .GetSection("OpsPortal:Services")
        .Get<List<MonitoredService>>()
        ?? [];

    var client = httpClientFactory.CreateClient();
    var results = new List<object>();

    foreach (var service in services)
    {
        var startedAt = DateTimeOffset.UtcNow;
        try
        {
            var url = $"{service.BaseUrl.TrimEnd('/')}/ops/dashboard/overview";
            var response = await client.GetAsync(url, cancellationToken);
            var latencyMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;

            if (!response.IsSuccessStatusCode)
            {
                results.Add(new
                {
                    service.Name,
                    service.BaseUrl,
                    Status = "degraded",
                    LatencyMs = Math.Round(latencyMs, 2),
                    Error = $"{(int)response.StatusCode} {response.ReasonPhrase}"
                });
                continue;
            }

            var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>(cancellationToken: cancellationToken);
            results.Add(new
            {
                service.Name,
                service.BaseUrl,
                Status = "healthy",
                LatencyMs = Math.Round(latencyMs, 2),
                Data = payload
            });
        }
        catch (Exception ex)
        {
            var latencyMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
            results.Add(new
            {
                service.Name,
                service.BaseUrl,
                Status = "down",
                LatencyMs = Math.Round(latencyMs, 2),
                Error = ex.Message
            });
        }
    }

    return Results.Ok(new
    {
        GeneratedAt = DateTimeOffset.UtcNow,
        Services = results
    });
});

app.Run();

internal sealed class MonitoredService
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
}
