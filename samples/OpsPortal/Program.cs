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
            var baseUrl = service.BaseUrl.TrimEnd('/');
            var liveUrl = $"{baseUrl}/health/live";
            var liveResponse = await client.GetAsync(liveUrl, cancellationToken);
            var latencyMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;

            if (!liveResponse.IsSuccessStatusCode)
            {
                results.Add(new
                {
                    service.Name,
                    service.BaseUrl,
                    Status = "degraded",
                    LatencyMs = Math.Round(latencyMs, 2),
                    Error = $"{(int)liveResponse.StatusCode} {liveResponse.ReasonPhrase}"
                });
                continue;
            }

            Dictionary<string, object?>? payload = null;
            try
            {
                var overviewUrl = $"{baseUrl}/ops/dashboard/overview";
                var overviewResponse = await client.GetAsync(overviewUrl, cancellationToken);
                if (overviewResponse.IsSuccessStatusCode)
                {
                    payload = await overviewResponse.Content.ReadFromJsonAsync<Dictionary<string, object?>>(cancellationToken: cancellationToken);
                }
            }
            catch
            {
                // Keep service healthy when liveness is OK; dashboard metrics are optional.
            }

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
