using System.Net.Http.Json;
using System.Text;
using System.Threading.RateLimiting;
using Dapr.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprClient();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddHttpClient();
var securityOptions = builder.Configuration.GetSection("Security").Get<GatewaySecurityOptions>() ?? new GatewaySecurityOptions();
builder.Services.AddSingleton(securityOptions);
builder.Services.AddSingleton<ClientBlacklist>();
builder.Services.AddSingleton<RateLimitMetricsStore>();
builder.Services.AddSingleton<RuntimeSecurityConfig>();
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var response = context.HttpContext.Response;
        response.ContentType = "application/json";
        var correlationId = context.HttpContext.TraceIdentifier;
        var metricsStore = context.HttpContext.RequestServices.GetRequiredService<RateLimitMetricsStore>();
        var clientKey = ClientIdentityResolver.Resolve(context.HttpContext);
        await metricsStore.RecordRejectionAsync(clientKey, context.HttpContext.RequestServices.GetRequiredService<DaprClient>());
        await response.WriteAsJsonAsync(new
        {
            errorCode = "rate_limited",
            message = "Too many requests. Please retry later.",
            correlationId
        }, cancellationToken: cancellationToken);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var runtimeSecurity = httpContext.RequestServices.GetRequiredService<RuntimeSecurityConfig>();
        if (runtimeSecurity.IsPathExempt(httpContext.Request.Path))
        {
            return RateLimitPartition.GetNoLimiter("exempt");
        }

        var key = ClientIdentityResolver.Resolve(httpContext);
        var policy = runtimeSecurity.ResolveRateLimitPolicy(httpContext.Request.Path, httpContext.Request.Method);
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{policy.Key}:{key}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = policy.PermitLimit,
                Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                QueueLimit = policy.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });
});
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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var blacklist = scope.ServiceProvider.GetRequiredService<ClientBlacklist>();
    var metricsStore = scope.ServiceProvider.GetRequiredService<RateLimitMetricsStore>();
    var runtimeSecurity = scope.ServiceProvider.GetRequiredService<RuntimeSecurityConfig>();
    var daprClient = scope.ServiceProvider.GetRequiredService<DaprClient>();
    await blacklist.LoadAsync(daprClient);
    await metricsStore.LoadAsync(daprClient);
    await runtimeSecurity.LoadAsync(daprClient);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Ok(new { service = "GatewayService", status = "ok" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));

app.Use(async (context, next) =>
{
    var blacklist = context.RequestServices.GetRequiredService<ClientBlacklist>();
    var metricsStore = context.RequestServices.GetRequiredService<RateLimitMetricsStore>();
    var daprClient = context.RequestServices.GetRequiredService<DaprClient>();
    var clientKey = ClientIdentityResolver.Resolve(context);
    await metricsStore.RecordRequestAsync(clientKey, daprClient);
    if (blacklist.IsBlocked(clientKey))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            errorCode = "client_blocked",
            message = "Client is blocked by gateway blacklist.",
            client = clientKey,
            correlationId = context.TraceIdentifier
        });
        return;
    }

    await next();
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/gateway/routes", () => Results.Ok(new
{
    routes = new[]
    {
        new { prefix = "/api/gw/auth", target = "authservice" },
        new { prefix = "/api/gw/users", target = "userservice" },
        new { prefix = "/api/gw/orders", target = "orderservice" },
        new { prefix = "/api/gw/catalog", target = "catalogservice" },
        new { prefix = "/api/gw/inventory", target = "inventoryservice" },
        new { prefix = "/api/gw/promotions", target = "promotionservice" },
        new { prefix = "/api/gw/notifications", target = "notificationservice" },
        new { prefix = "/api/gw/jobs", target = "jobservice" }
    }
}));

app.MapGet("/api/gw/security/blacklist", (ClientBlacklist blacklist) =>
{
    return Results.Ok(new { items = blacklist.GetAll() });
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/gw/security/blacklist", async (BlacklistUpsertRequest request, ClientBlacklist blacklist, DaprClient daprClient) =>
{
    if (string.IsNullOrWhiteSpace(request.Client))
    {
        return Results.BadRequest(new { message = "client is required." });
    }

    blacklist.Add(request.Client, request.TtlMinutes);
    await blacklist.SaveAsync(daprClient);
    return Results.Ok(new { blocked = request.Client, ttlMinutes = request.TtlMinutes });
}).RequireAuthorization("AdminOnly");

app.MapDelete("/api/gw/security/blacklist/{client}", async (string client, ClientBlacklist blacklist, DaprClient daprClient) =>
{
    blacklist.Remove(client);
    await blacklist.SaveAsync(daprClient);
    return Results.NoContent();
}).RequireAuthorization("AdminOnly");

app.MapGet("/api/gw/security/rate-limit-metrics", (int take, RateLimitMetricsStore metricsStore) =>
{
    var safeTake = take <= 0 ? 50 : Math.Min(take, 200);
    return Results.Ok(new { items = metricsStore.GetTop(safeTake) });
}).RequireAuthorization("AdminOnly");

app.MapGet("/api/gw/security/rate-limit-metrics/{client}", (string client, RateLimitMetricsStore metricsStore) =>
{
    var item = metricsStore.Get(client);
    return item is null ? Results.NotFound() : Results.Ok(item);
}).RequireAuthorization("AdminOnly");

app.MapDelete("/api/gw/security/rate-limit-metrics/{client}", async (string client, RateLimitMetricsStore metricsStore, DaprClient daprClient) =>
{
    var removed = await metricsStore.RemoveAsync(client, daprClient);
    return removed ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/gw/security/rate-limit-metrics/reset", async (RateLimitMetricsStore metricsStore, DaprClient daprClient) =>
{
    await metricsStore.ClearAsync(daprClient);
    return Results.NoContent();
}).RequireAuthorization("AdminOnly");

app.MapDelete("/api/gw/security/blacklist", async (ClientBlacklist blacklist, DaprClient daprClient) =>
{
    await blacklist.ClearAsync(daprClient);
    return Results.NoContent();
}).RequireAuthorization("AdminOnly");

app.MapGet("/api/gw/security/overview", (ClientBlacklist blacklist, RateLimitMetricsStore metricsStore) =>
{
    return Results.Ok(new
    {
        blockedClients = blacklist.GetAll().Count,
        observedClients = metricsStore.Count,
        topRejectedClients = metricsStore.GetTop(10)
    });
}).RequireAuthorization("AdminOnly");

app.MapGet("/api/gw/security/config", (RuntimeSecurityConfig runtimeSecurity) =>
{
    return Results.Ok(runtimeSecurity.GetSnapshot());
}).RequireAuthorization("AdminOnly");

app.MapPut("/api/gw/security/config/rate-limit-policies/{policyKey}", async (string policyKey, UpdateRateLimitPolicyRequest request, RuntimeSecurityConfig runtimeSecurity, DaprClient daprClient) =>
{
    runtimeSecurity.UpsertPolicy(policyKey, new RateLimitPolicy(policyKey, request.PermitLimit, request.WindowSeconds, request.QueueLimit));
    await runtimeSecurity.SaveAsync(daprClient);
    return Results.Ok(runtimeSecurity.GetSnapshot());
}).RequireAuthorization("AdminOnly");

app.MapPut("/api/gw/security/config/exempt-paths", async (UpdateExemptPathsRequest request, RuntimeSecurityConfig runtimeSecurity, DaprClient daprClient) =>
{
    runtimeSecurity.SetExemptPaths(request.Paths ?? []);
    await runtimeSecurity.SaveAsync(daprClient);
    return Results.Ok(runtimeSecurity.GetSnapshot());
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/gw/auth/login", async (HttpContext httpContext, ForwardLoginRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, "http://authservice:8080/api/auth/login", request, cancellationToken);
});

app.MapPost("/api/gw/auth/register", async (HttpContext httpContext, ForwardRegisterRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, "http://authservice:8080/api/auth/register", request, cancellationToken);
});

app.MapGet("/api/gw/users/{id:guid}", async (HttpContext httpContext, Guid id, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, $"http://userservice:8080/api/users/{id}"), cancellationToken);
}).RequireAuthorization();

app.MapPatch("/api/gw/users/{id:guid}/roles", async (HttpContext httpContext, Guid id, ForwardRolesRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPatchAsync(httpContext, httpClientFactory, $"http://userservice:8080/api/users/{id}/roles", request, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPatch("/api/gw/users/{id:guid}/status", async (HttpContext httpContext, Guid id, ForwardStatusRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPatchAsync(httpContext, httpClientFactory, $"http://userservice:8080/api/users/{id}/status", request, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapGet("/api/gw/users/me/addresses", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, "http://userservice:8080/api/users/me/addresses"), cancellationToken);
}).RequireAuthorization();

app.MapPost("/api/gw/users/me/addresses", async (HttpContext httpContext, ForwardCreateUserAddressRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, "http://userservice:8080/api/users/me/addresses", request, cancellationToken);
}).RequireAuthorization();

app.MapPatch("/api/gw/users/me/addresses/{addressId:guid}", async (HttpContext httpContext, Guid addressId, ForwardUpdateUserAddressRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPatchAsync(httpContext, httpClientFactory, $"http://userservice:8080/api/users/me/addresses/{addressId}", request, cancellationToken);
}).RequireAuthorization();

app.MapDelete("/api/gw/users/me/addresses/{addressId:guid}", async (HttpContext httpContext, Guid addressId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardDeleteAsync(httpContext, httpClientFactory, $"http://userservice:8080/api/users/me/addresses/{addressId}", cancellationToken);
}).RequireAuthorization();

app.MapPost("/api/gw/orders", async (HttpContext httpContext, ForwardCreateOrderRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, "http://orderservice:8080/api/orders", request, cancellationToken);
}).RequireAuthorization();

app.MapPost("/api/gw/orders/checkout", async (HttpContext httpContext, ForwardCheckoutCartRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, "http://orderservice:8080/api/orders/checkout", request, cancellationToken);
}).RequireAuthorization();

app.MapPost("/api/gw/orders/{orderId}/pay", async (HttpContext httpContext, string orderId, ForwardSimulatePayRequest? request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    var body = request ?? new ForwardSimulatePayRequest(null);
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, $"http://orderservice:8080/api/orders/{Uri.EscapeDataString(orderId)}/pay", body, cancellationToken);
}).RequireAuthorization();

app.MapPost("/api/gw/orders/payments/callback", async (HttpContext httpContext, ForwardPaymentCallbackRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, "http://orderservice:8080/api/orders/payments/callback", request, cancellationToken);
});

app.MapPost("/api/gw/orders/ops/expire-awaiting-payments", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync<object?>(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, "http://orderservice:8080/api/orders/ops/expire-awaiting-payments"), null, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/gw/orders/{orderId}/sub-orders/{subOrderId}/ship", async (HttpContext httpContext, string orderId, string subOrderId, ForwardSubOrderShipRequest? request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    var body = request ?? new ForwardSubOrderShipRequest(null);
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, $"http://orderservice:8080/api/orders/{Uri.EscapeDataString(orderId)}/sub-orders/{Uri.EscapeDataString(subOrderId)}/ship", body, cancellationToken);
}).RequireAuthorization();

app.MapPost("/api/gw/orders/{orderId}/sub-orders/{subOrderId}/deliver", async (HttpContext httpContext, string orderId, string subOrderId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync<object?>(httpContext, httpClientFactory, $"http://orderservice:8080/api/orders/{Uri.EscapeDataString(orderId)}/sub-orders/{Uri.EscapeDataString(subOrderId)}/deliver", null, cancellationToken);
}).RequireAuthorization();

app.MapGet("/api/gw/orders/{orderId}/sub-orders/{subOrderId}/tracking", async (HttpContext httpContext, string orderId, string subOrderId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, $"http://orderservice:8080/api/orders/{Uri.EscapeDataString(orderId)}/sub-orders/{Uri.EscapeDataString(subOrderId)}/tracking"), cancellationToken);
}).RequireAuthorization();

app.MapPost("/api/gw/orders/{orderId}/cancel", async (HttpContext httpContext, string orderId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync<object?>(httpContext, httpClientFactory, $"http://orderservice:8080/api/orders/{Uri.EscapeDataString(orderId)}/cancel", null, cancellationToken);
}).RequireAuthorization();

app.MapPost("/api/gw/orders/{orderId}/sub-orders/{subOrderId}/cancel", async (HttpContext httpContext, string orderId, string subOrderId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync<object?>(httpContext, httpClientFactory, $"http://orderservice:8080/api/orders/{Uri.EscapeDataString(orderId)}/sub-orders/{Uri.EscapeDataString(subOrderId)}/cancel", null, cancellationToken);
}).RequireAuthorization();

app.MapGet("/api/gw/orders/{orderId}/after-sales", async (HttpContext httpContext, string orderId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, $"http://orderservice:8080/api/orders/{Uri.EscapeDataString(orderId)}/after-sales"), cancellationToken);
}).RequireAuthorization();

app.MapPost("/api/gw/orders/{orderId}/after-sales", async (HttpContext httpContext, string orderId, ForwardCreateAfterSaleRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, $"http://orderservice:8080/api/orders/{Uri.EscapeDataString(orderId)}/after-sales", request, cancellationToken);
}).RequireAuthorization();

app.MapPost("/api/gw/orders/{orderId}/after-sales/{afterSaleId}/approve", async (HttpContext httpContext, string orderId, string afterSaleId, ForwardReviewAfterSaleRequest? request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    var body = request ?? new ForwardReviewAfterSaleRequest(null);
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, $"http://orderservice:8080/api/orders/{Uri.EscapeDataString(orderId)}/after-sales/{Uri.EscapeDataString(afterSaleId)}/approve", body, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/gw/orders/{orderId}/after-sales/{afterSaleId}/reject", async (HttpContext httpContext, string orderId, string afterSaleId, ForwardReviewAfterSaleRequest? request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    var body = request ?? new ForwardReviewAfterSaleRequest(null);
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, $"http://orderservice:8080/api/orders/{Uri.EscapeDataString(orderId)}/after-sales/{Uri.EscapeDataString(afterSaleId)}/reject", body, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapGet("/api/gw/orders/by-shop/{shopId}/after-sales", async (HttpContext httpContext, string shopId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, $"http://orderservice:8080/api/orders/by-shop/{Uri.EscapeDataString(shopId)}/after-sales"), cancellationToken);
}).RequireAuthorization();

app.MapGet("/api/gw/orders/by-shop/{shopId}/after-sales/search", async (HttpContext httpContext, string shopId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, $"http://orderservice:8080/api/orders/by-shop/{Uri.EscapeDataString(shopId)}/after-sales/search"), cancellationToken);
}).RequireAuthorization();

app.MapGet("/api/gw/orders/me", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, "http://orderservice:8080/api/orders/me"), cancellationToken);
}).RequireAuthorization();

app.MapGet("/api/gw/orders/me/search", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, "http://orderservice:8080/api/orders/me/search"), cancellationToken);
}).RequireAuthorization();

app.MapGet("/api/gw/orders/by-user/{userId}", async (HttpContext httpContext, string userId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, $"http://orderservice:8080/api/orders/by-user/{Uri.EscapeDataString(userId)}"), cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapGet("/api/gw/orders/by-user/{userId}/search", async (HttpContext httpContext, string userId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, $"http://orderservice:8080/api/orders/by-user/{Uri.EscapeDataString(userId)}/search"), cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapGet("/api/gw/orders/by-shop/{shopId}", async (HttpContext httpContext, string shopId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, $"http://orderservice:8080/api/orders/by-shop/{Uri.EscapeDataString(shopId)}"), cancellationToken);
}).RequireAuthorization();

app.MapGet("/api/gw/orders/by-shop/{shopId}/search", async (HttpContext httpContext, string shopId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, $"http://orderservice:8080/api/orders/by-shop/{Uri.EscapeDataString(shopId)}/search"), cancellationToken);
}).RequireAuthorization();

app.MapGet("/api/gw/orders/{id}", async (HttpContext httpContext, string id, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, $"http://orderservice:8080/api/orders/{id}"), cancellationToken);
}).RequireAuthorization();

app.MapGet("/api/gw/carts/me", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, "http://orderservice:8080/api/carts/me"), cancellationToken);
}).RequireAuthorization();

app.MapPut("/api/gw/carts/me", async (HttpContext httpContext, ForwardReplaceCartRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPutAsync(httpContext, httpClientFactory, "http://orderservice:8080/api/carts/me", request, cancellationToken);
}).RequireAuthorization();

app.MapGet("/api/gw/catalog/items", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, "http://catalogservice:8080/api/catalog/items"), cancellationToken);
}).RequireAuthorization();

app.MapGet("/api/gw/catalog/items/{id}", async (HttpContext httpContext, string id, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, $"http://catalogservice:8080/api/catalog/items/{id}"), cancellationToken);
}).RequireAuthorization();

app.MapPut("/api/gw/catalog/items/{id}", async (HttpContext httpContext, string id, ForwardCatalogUpsertRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPutAsync(httpContext, httpClientFactory, $"http://catalogservice:8080/api/catalog/items/{id}", request, cancellationToken);
}).RequireAuthorization();

app.MapPost("/api/gw/catalog/items/{id}/submit", async (HttpContext httpContext, string id, ForwardCatalogSubmitRequest? request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    var body = request ?? new ForwardCatalogSubmitRequest(null);
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, $"http://catalogservice:8080/api/catalog/items/{id}/submit", body, cancellationToken);
}).RequireAuthorization();

app.MapPost("/api/gw/catalog/items/{id}/approve", async (HttpContext httpContext, string id, ForwardCatalogApproveRequest? request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    var body = request ?? new ForwardCatalogApproveRequest(null);
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, $"http://catalogservice:8080/api/catalog/items/{id}/approve", body, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/gw/catalog/items/{id}/reject", async (HttpContext httpContext, string id, ForwardCatalogRejectRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, $"http://catalogservice:8080/api/catalog/items/{id}/reject", request, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapGet("/api/gw/catalog/items/{id}/audit-history", async (HttpContext httpContext, string id, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, $"http://catalogservice:8080/api/catalog/items/{id}/audit-history", cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/gw/catalog/items/{id}/shelf", async (HttpContext httpContext, string id, ForwardCatalogSetShelfRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, $"http://catalogservice:8080/api/catalog/items/{id}/shelf", request, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/gw/catalog/items/{id}/shelf-schedule", async (HttpContext httpContext, string id, ForwardCatalogSetShelfScheduleRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, $"http://catalogservice:8080/api/catalog/items/{id}/shelf-schedule", request, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/gw/catalog/ops/apply-shelf-schedules", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync<object?>(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, "http://catalogservice:8080/api/catalog/ops/apply-shelf-schedules"), null, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapGet("/api/gw/catalog/categories", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, "http://catalogservice:8080/api/catalog/categories", cancellationToken);
}).RequireAuthorization();

app.MapPost("/api/gw/catalog/categories/{id}", async (HttpContext httpContext, string id, ForwardCatalogCategoryUpsertRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, $"http://catalogservice:8080/api/catalog/categories/{id}", request, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPatch("/api/gw/catalog/categories/{id}/sort", async (HttpContext httpContext, string id, ForwardCatalogCategorySortRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPatchAsync(httpContext, httpClientFactory, $"http://catalogservice:8080/api/catalog/categories/{id}/sort", request, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPatch("/api/gw/catalog/categories/{id}/visibility", async (HttpContext httpContext, string id, ForwardCatalogCategoryVisibilityRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPatchAsync(httpContext, httpClientFactory, $"http://catalogservice:8080/api/catalog/categories/{id}/visibility", request, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPatch("/api/gw/catalog/categories/{id}/enabled", async (HttpContext httpContext, string id, ForwardCatalogCategoryEnabledRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPatchAsync(httpContext, httpClientFactory, $"http://catalogservice:8080/api/catalog/categories/{id}/enabled", request, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapDelete("/api/gw/catalog/categories/{id}", async (HttpContext httpContext, string id, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardDeleteAsync(httpContext, httpClientFactory, $"http://catalogservice:8080/api/catalog/categories/{id}", cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapGet("/api/gw/inventory/{productId}", async (HttpContext httpContext, string productId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, $"http://inventoryservice:8080/api/inventory/{productId}"), cancellationToken);
}).RequireAuthorization();

app.MapPut("/api/gw/inventory/{productId}", async (HttpContext httpContext, string productId, ForwardInventorySetRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPutAsync(httpContext, httpClientFactory, $"http://inventoryservice:8080/api/inventory/{productId}", request, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/gw/inventory/{productId}/reserve", async (HttpContext httpContext, string productId, ForwardInventoryChangeRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, $"http://inventoryservice:8080/api/inventory/{productId}/reserve", request, cancellationToken);
}).RequireAuthorization();

app.MapPost("/api/gw/inventory/{productId}/release", async (HttpContext httpContext, string productId, ForwardInventoryChangeRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, $"http://inventoryservice:8080/api/inventory/{productId}/release", request, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapGet("/api/gw/promotions", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, "http://promotionservice:8080/api/promotions"), cancellationToken);
}).RequireAuthorization();

app.MapGet("/api/gw/promotions/{code}", async (HttpContext httpContext, string code, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, $"http://promotionservice:8080/api/promotions/{code}"), cancellationToken);
}).RequireAuthorization();

app.MapPost("/api/gw/promotions", async (HttpContext httpContext, ForwardCreatePromotionRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, "http://promotionservice:8080/api/promotions", request, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/gw/promotions/validate", async (HttpContext httpContext, ForwardValidatePromotionRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, "http://promotionservice:8080/api/promotions/validate", request, cancellationToken);
}).RequireAuthorization();

app.MapPost("/api/gw/notifications/send", async (HttpContext httpContext, ForwardSendNotificationRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync(httpContext, httpClientFactory, "http://notificationservice:8080/api/notifications/send", request, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapGet("/api/gw/notifications", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, "http://notificationservice:8080/api/notifications"), cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/gw/jobs/run/reconcile-inventory", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync<object?>(httpContext, httpClientFactory, "http://jobservice:8080/api/jobs/run/reconcile-inventory", null, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/gw/jobs/run/replay-audit", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync<object?>(httpContext, httpClientFactory, "http://jobservice:8080/api/jobs/run/replay-audit", null, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/gw/jobs/run/expire-awaiting-payments", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync<object?>(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, "http://jobservice:8080/api/jobs/run/expire-awaiting-payments"), null, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/gw/jobs/run/reconcile-refunds", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync<object?>(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, "http://jobservice:8080/api/jobs/run/reconcile-refunds"), null, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/gw/jobs/run/reclaim-expired-inventory-reservations", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync<object?>(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, "http://jobservice:8080/api/jobs/run/reclaim-expired-inventory-reservations"), null, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/gw/jobs/run/apply-catalog-shelf-schedules", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardPostAsync<object?>(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, "http://jobservice:8080/api/jobs/run/apply-catalog-shelf-schedules"), null, cancellationToken);
}).RequireAuthorization("AdminOnly");

app.MapGet("/api/gw/jobs/runs", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await GatewayForwarder.ForwardGetAsync(httpContext, httpClientFactory, GatewayForwarder.AppendIncomingQuery(httpContext, "http://jobservice:8080/api/jobs/runs"), cancellationToken);
}).RequireAuthorization("AdminOnly");

app.Run();

internal sealed record ForwardLoginRequest(string Account, string Password);
internal sealed record ForwardRegisterRequest(string Username, string Email, string Password);
internal sealed record ForwardRolesRequest(string[] Roles);
internal sealed record ForwardStatusRequest(string Status);
internal sealed record ForwardCatalogUpsertRequest(string Name, decimal Price, bool IsActive, string? ShopId, string? CategoryId, IReadOnlyList<ForwardCatalogSkuRequest>? Skus);
internal sealed record ForwardCatalogSkuRequest(string SkuId, string Name, decimal? Price, bool IsActive);
internal sealed record ForwardCatalogSubmitRequest(string? Note);
internal sealed record ForwardCatalogApproveRequest(string? Note);
internal sealed record ForwardCatalogRejectRequest(string Reason, string? Note);
internal sealed record ForwardCatalogSetShelfRequest(bool IsOnShelf);
internal sealed record ForwardCatalogSetShelfScheduleRequest(DateTimeOffset? OnAt, DateTimeOffset? OffAt);
internal sealed record ForwardCatalogCategoryUpsertRequest(string Name, string? ParentId, int SortOrder, bool IsVisible, bool IsEnabled);
internal sealed record ForwardCatalogCategorySortRequest(int SortOrder);
internal sealed record ForwardCatalogCategoryVisibilityRequest(bool IsVisible);
internal sealed record ForwardCatalogCategoryEnabledRequest(bool IsEnabled);
internal sealed record ForwardInventorySetRequest(int Quantity);
internal sealed record ForwardInventoryChangeRequest(int Quantity);
internal sealed record ForwardCreatePromotionRequest(
    string Code,
    string Name,
    string DiscountType,
    decimal DiscountValue,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    bool IsEnabled);
internal sealed record ForwardValidatePromotionRequest(string Code, decimal OrderAmount);
internal sealed record ForwardSendNotificationRequest(string Channel, string To, string Title, string Body);
internal sealed record ForwardCreateOrderRequest(string ProductId, string? SkuId, int Quantity, string? PromoCode, string? UserId = null);
internal sealed record ForwardCheckoutCartRequest(string? PromoCode, Guid? AddressId);
internal sealed record ForwardCreateUserAddressRequest(string ReceiverName, string Phone, string Region, string Detail, bool IsDefault);
internal sealed record ForwardUpdateUserAddressRequest(string? ReceiverName, string? Phone, string? Region, string? Detail, bool? IsDefault);
internal sealed record ForwardSimulatePayRequest(string? IdempotencyKey);
internal sealed record ForwardPaymentCallbackRequest(string CallbackId, string OrderId, string TransactionId, decimal Amount, string Status, DateTimeOffset PaidAt);
internal sealed record ForwardSubOrderShipRequest(string? TrackingNumber, string? CarrierCode = null, string? CarrierName = null);
internal sealed record ForwardCreateAfterSaleRequest(string? SubOrderId, string Reason, string? Detail, decimal? RequestedAmount);
internal sealed record ForwardReviewAfterSaleRequest(string? Note);
internal sealed record ForwardReplaceCartRequest(IReadOnlyList<ForwardCartLineDto> Lines);
internal sealed record ForwardCartLineDto(string ProductId, string? SkuId, int Quantity);
internal sealed record BlacklistUpsertRequest(string Client, int? TtlMinutes);
internal sealed record UpdateRateLimitPolicyRequest(int PermitLimit, int WindowSeconds, int QueueLimit);
internal sealed record UpdateExemptPathsRequest(string[] Paths);

internal sealed class GatewaySecurityOptions
{
    public RateLimitOptions RateLimit { get; init; } = new();
    public Dictionary<string, RateLimitPolicy> RateLimitPolicies { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string[] Blacklist { get; init; } = [];
    public string[] RateLimitExemptPaths { get; init; } =
    [
        "/",
        "/health/live",
        "/health/ready"
    ];

    public bool IsPathExempt(string path)
    {
        return RateLimitExemptPaths.Any(x =>
            path.Equals(x, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(x.EndsWith('/') ? x : $"{x}/", StringComparison.OrdinalIgnoreCase));
    }

    public RateLimitPolicy ResolveRateLimitPolicy(string path, string method)
    {
        if (path.StartsWith("/api/gw/auth", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPolicies.GetValueOrDefault("auth") ?? new RateLimitPolicy("auth", 20, 60, 0);
        }

        if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method))
        {
            return RateLimitPolicies.GetValueOrDefault("write") ?? new RateLimitPolicy("write", 30, 60, 0);
        }

        return RateLimitPolicies.GetValueOrDefault("read") ?? new RateLimitPolicy("read", RateLimit.PermitLimit, RateLimit.WindowSeconds, RateLimit.QueueLimit);
    }
}

internal sealed class RuntimeSecurityConfig
{
    private const string StateStoreName = "statestore";
    private const string StateKey = "gateway:security:runtime-config";
    private readonly object _gate = new();
    private Dictionary<string, RateLimitPolicy> _policies;
    private string[] _exemptPaths;

    public RuntimeSecurityConfig(GatewaySecurityOptions options)
    {
        _policies = new Dictionary<string, RateLimitPolicy>(options.RateLimitPolicies, StringComparer.OrdinalIgnoreCase);
        _exemptPaths = options.RateLimitExemptPaths;
        if (!_policies.ContainsKey("auth"))
        {
            _policies["auth"] = new RateLimitPolicy("auth", 20, 60, 0);
        }

        if (!_policies.ContainsKey("write"))
        {
            _policies["write"] = new RateLimitPolicy("write", 30, 60, 0);
        }

        if (!_policies.ContainsKey("read"))
        {
            _policies["read"] = new RateLimitPolicy("read", options.RateLimit.PermitLimit, options.RateLimit.WindowSeconds, options.RateLimit.QueueLimit);
        }
    }

    public async Task LoadAsync(DaprClient daprClient)
    {
        var persisted = await daprClient.GetStateAsync<RuntimeSecurityConfigState>(StateStoreName, StateKey);
        if (persisted is null)
        {
            return;
        }

        lock (_gate)
        {
            _policies = new Dictionary<string, RateLimitPolicy>(persisted.Policies ?? new Dictionary<string, RateLimitPolicy>(), StringComparer.OrdinalIgnoreCase);
            _exemptPaths = persisted.ExemptPaths ?? [];
        }
    }

    public async Task SaveAsync(DaprClient daprClient)
    {
        RuntimeSecurityConfigState snapshot;
        lock (_gate)
        {
            snapshot = new RuntimeSecurityConfigState(new Dictionary<string, RateLimitPolicy>(_policies, StringComparer.OrdinalIgnoreCase), _exemptPaths);
        }

        await daprClient.SaveStateAsync(StateStoreName, StateKey, snapshot);
    }

    public bool IsPathExempt(string path)
    {
        lock (_gate)
        {
            return _exemptPaths.Any(x =>
                path.Equals(x, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(x.EndsWith('/') ? x : $"{x}/", StringComparison.OrdinalIgnoreCase));
        }
    }

    public RateLimitPolicy ResolveRateLimitPolicy(string path, string method)
    {
        lock (_gate)
        {
            if (path.StartsWith("/api/gw/auth", StringComparison.OrdinalIgnoreCase))
            {
                return _policies.GetValueOrDefault("auth") ?? new RateLimitPolicy("auth", 20, 60, 0);
            }

            if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method))
            {
                return _policies.GetValueOrDefault("write") ?? new RateLimitPolicy("write", 30, 60, 0);
            }

            return _policies.GetValueOrDefault("read") ?? new RateLimitPolicy("read", 120, 60, 0);
        }
    }

    public void UpsertPolicy(string key, RateLimitPolicy policy)
    {
        lock (_gate)
        {
            _policies[key] = policy with { Key = key };
        }
    }

    public void SetExemptPaths(string[] paths)
    {
        lock (_gate)
        {
            _exemptPaths = paths.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    public object GetSnapshot()
    {
        lock (_gate)
        {
            return new
            {
                policies = _policies,
                exemptPaths = _exemptPaths
            };
        }
    }
}

internal sealed record RuntimeSecurityConfigState(Dictionary<string, RateLimitPolicy> Policies, string[] ExemptPaths);

internal sealed class RateLimitOptions
{
    public int PermitLimit { get; init; } = 60;
    public int WindowSeconds { get; init; } = 60;
    public int QueueLimit { get; init; } = 0;
}

internal sealed record RateLimitPolicy(string Key, int PermitLimit, int WindowSeconds, int QueueLimit);

internal sealed class ClientBlacklist
{
    private const string StateStoreName = "statestore";
    private const string StateKey = "gateway:blacklist";
    private readonly Dictionary<string, BlacklistEntry> _clients;
    private readonly object _gate = new();

    public ClientBlacklist(GatewaySecurityOptions options)
    {
        _clients = new Dictionary<string, BlacklistEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var client in options.Blacklist ?? [])
        {
            _clients[client] = new BlacklistEntry(client, null, DateTimeOffset.UtcNow);
        }
    }

    public bool IsBlocked(string client)
    {
        lock (_gate)
        {
            if (!_clients.TryGetValue(client, out var entry))
            {
                return false;
            }

            if (entry.ExpiresAt.HasValue && entry.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                _clients.Remove(client);
                return false;
            }

            return true;
        }
    }

    public void Add(string client, int? ttlMinutes)
    {
        DateTimeOffset? expiresAt = ttlMinutes is > 0 ? DateTimeOffset.UtcNow.AddMinutes(ttlMinutes.Value) : null;
        lock (_gate)
        {
            _clients[client] = new BlacklistEntry(client, expiresAt, DateTimeOffset.UtcNow);
        }
    }

    public void Remove(string client)
    {
        lock (_gate)
        {
            _clients.Remove(client);
        }
    }

    public IReadOnlyCollection<BlacklistEntry> GetAll()
    {
        lock (_gate)
        {
            PruneExpired();
            return _clients.Values.OrderBy(x => x.Client).ToArray();
        }
    }

    public async Task LoadAsync(DaprClient daprClient)
    {
        var persisted = await daprClient.GetStateAsync<List<BlacklistEntry>>(StateStoreName, StateKey);
        if (persisted is null)
        {
            return;
        }

        lock (_gate)
        {
            _clients.Clear();
            foreach (var item in persisted)
            {
                _clients[item.Client] = item;
            }
            PruneExpired();
        }
    }

    public async Task SaveAsync(DaprClient daprClient)
    {
        List<BlacklistEntry> snapshot;
        lock (_gate)
        {
            PruneExpired();
            snapshot = _clients.Values.OrderBy(x => x.Client).ToList();
        }

        await daprClient.SaveStateAsync(StateStoreName, StateKey, snapshot);
    }

    public async Task ClearAsync(DaprClient daprClient)
    {
        lock (_gate)
        {
            _clients.Clear();
        }

        await daprClient.SaveStateAsync(StateStoreName, StateKey, new List<BlacklistEntry>());
    }

    private void PruneExpired()
    {
        var expired = _clients
            .Where(x => x.Value.ExpiresAt.HasValue && x.Value.ExpiresAt <= DateTimeOffset.UtcNow)
            .Select(x => x.Key)
            .ToArray();
        foreach (var key in expired)
        {
            _clients.Remove(key);
        }
    }
}

internal sealed record BlacklistEntry(string Client, DateTimeOffset? ExpiresAt, DateTimeOffset CreatedAt);

internal sealed class RateLimitMetricsStore
{
    private const string StateStoreName = "statestore";
    private const string StateKey = "gateway:rate-limit-metrics";
    private readonly Dictionary<string, RateLimitMetric> _metrics = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _metrics.Count;
            }
        }
    }

    public async Task LoadAsync(DaprClient daprClient)
    {
        var persisted = await daprClient.GetStateAsync<List<RateLimitMetric>>(StateStoreName, StateKey) ?? [];
        lock (_gate)
        {
            _metrics.Clear();
            foreach (var item in persisted)
            {
                _metrics[item.Client] = item;
            }
        }
    }

    public async Task RecordRequestAsync(string client, DaprClient daprClient)
    {
        lock (_gate)
        {
            if (!_metrics.TryGetValue(client, out var metric))
            {
                metric = new RateLimitMetric(client, 0, 0, DateTimeOffset.UtcNow);
            }

            _metrics[client] = metric with
            {
                RequestCount = metric.RequestCount + 1,
                LastSeenAt = DateTimeOffset.UtcNow
            };
        }

        await SaveAsync(daprClient);
    }

    public async Task RecordRejectionAsync(string client, DaprClient daprClient)
    {
        lock (_gate)
        {
            if (!_metrics.TryGetValue(client, out var metric))
            {
                metric = new RateLimitMetric(client, 0, 0, DateTimeOffset.UtcNow);
            }

            _metrics[client] = metric with
            {
                RejectionCount = metric.RejectionCount + 1,
                LastSeenAt = DateTimeOffset.UtcNow
            };
        }

        await SaveAsync(daprClient);
    }

    public IReadOnlyCollection<RateLimitMetric> GetTop(int take)
    {
        lock (_gate)
        {
            return _metrics.Values
                .OrderByDescending(x => x.RejectionCount)
                .ThenByDescending(x => x.RequestCount)
                .Take(take)
                .ToArray();
        }
    }

    public RateLimitMetric? Get(string client)
    {
        lock (_gate)
        {
            return _metrics.TryGetValue(client, out var metric) ? metric : null;
        }
    }

    public async Task<bool> RemoveAsync(string client, DaprClient daprClient)
    {
        bool removed;
        lock (_gate)
        {
            removed = _metrics.Remove(client);
        }

        if (removed)
        {
            await SaveAsync(daprClient);
        }

        return removed;
    }

    public async Task ClearAsync(DaprClient daprClient)
    {
        lock (_gate)
        {
            _metrics.Clear();
        }

        await daprClient.SaveStateAsync(StateStoreName, StateKey, new List<RateLimitMetric>());
    }

    private async Task SaveAsync(DaprClient daprClient)
    {
        List<RateLimitMetric> snapshot;
        lock (_gate)
        {
            snapshot = _metrics.Values.OrderByDescending(x => x.LastSeenAt).Take(1000).ToList();
        }

        await daprClient.SaveStateAsync(StateStoreName, StateKey, snapshot);
    }
}

internal sealed record RateLimitMetric(string Client, long RequestCount, long RejectionCount, DateTimeOffset LastSeenAt);

internal static class ClientIdentityResolver
{
    public static string Resolve(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("x-client-id", out var clientId) && !string.IsNullOrWhiteSpace(clientId))
        {
            return $"client:{clientId.ToString()}";
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ip}";
    }
}

internal static class GatewayForwarder
{
    public static string AppendIncomingQuery(HttpContext httpContext, string urlWithoutQuery) =>
        urlWithoutQuery + (httpContext.Request.QueryString.HasValue ? httpContext.Request.QueryString.Value : string.Empty);

    private static readonly HashSet<string> HeaderWhitelist = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "x-correlation-id",
        "x-request-id",
        "x-payment-signature",
        "x-payment-timestamp",
        "traceparent",
        "tracestate",
        "baggage"
    };

    public static async Task<IResult> ForwardGetAsync(HttpContext httpContext, IHttpClientFactory factory, string url, CancellationToken cancellationToken)
    {
        var request = BuildRequest(httpContext, HttpMethod.Get, url, null);
        return await SendAsync(factory, request, cancellationToken);
    }

    public static async Task<IResult> ForwardPostAsync<TBody>(HttpContext httpContext, IHttpClientFactory factory, string url, TBody body, CancellationToken cancellationToken)
    {
        var request = BuildRequest(httpContext, HttpMethod.Post, url, body);
        return await SendAsync(factory, request, cancellationToken);
    }

    public static async Task<IResult> ForwardPutAsync<TBody>(HttpContext httpContext, IHttpClientFactory factory, string url, TBody body, CancellationToken cancellationToken)
    {
        var request = BuildRequest(httpContext, HttpMethod.Put, url, body);
        return await SendAsync(factory, request, cancellationToken);
    }

    public static async Task<IResult> ForwardPatchAsync<TBody>(HttpContext httpContext, IHttpClientFactory factory, string url, TBody body, CancellationToken cancellationToken)
    {
        var request = BuildRequest(httpContext, HttpMethod.Patch, url, body);
        return await SendAsync(factory, request, cancellationToken);
    }

    public static async Task<IResult> ForwardDeleteAsync(HttpContext httpContext, IHttpClientFactory factory, string url, CancellationToken cancellationToken)
    {
        var request = BuildRequest(httpContext, HttpMethod.Delete, url, null);
        return await SendAsync(factory, request, cancellationToken);
    }

    private static HttpRequestMessage BuildRequest(HttpContext context, HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        CopyWhitelistedHeaders(context, request);
        EnsureCorrelationId(context, request);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static async Task<IResult> SendAsync(IHttpClientFactory factory, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var client = factory.CreateClient();
        try
        {
            var response = await client.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            return Results.Content(payload, "application/json", statusCode: (int)response.StatusCode);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return BuildGatewayErrorResult(
                statusCode: StatusCodes.Status504GatewayTimeout,
                errorCode: "gateway_timeout",
                message: "Gateway timed out while waiting for downstream service.",
                correlationId: ResolveCorrelationId(request),
                detail: ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return BuildGatewayErrorResult(
                statusCode: StatusCodes.Status502BadGateway,
                errorCode: "downstream_unavailable",
                message: "Gateway failed to reach downstream service.",
                correlationId: ResolveCorrelationId(request),
                detail: ex.Message);
        }
        catch (Exception ex)
        {
            return BuildGatewayErrorResult(
                statusCode: StatusCodes.Status502BadGateway,
                errorCode: "gateway_forwarding_failed",
                message: "Gateway forwarding failed.",
                correlationId: ResolveCorrelationId(request),
                detail: ex.Message);
        }
    }

    private static void CopyWhitelistedHeaders(HttpContext context, HttpRequestMessage request)
    {
        foreach (var header in context.Request.Headers)
        {
            if (!HeaderWhitelist.Contains(header.Key))
            {
                continue;
            }

            request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
    }

    private static void EnsureCorrelationId(HttpContext context, HttpRequestMessage request)
    {
        const string key = "x-correlation-id";
        if (request.Headers.Contains(key))
        {
            return;
        }

        var correlationId = context.TraceIdentifier;
        request.Headers.TryAddWithoutValidation(key, correlationId);
        context.Response.Headers[key] = correlationId;
    }

    private static string ResolveCorrelationId(HttpRequestMessage request)
    {
        if (request.Headers.TryGetValues("x-correlation-id", out var values))
        {
            return values.FirstOrDefault() ?? "unknown";
        }

        return "unknown";
    }

    private static IResult BuildGatewayErrorResult(int statusCode, string errorCode, string message, string correlationId, string detail)
    {
        return Results.Json(new
        {
            errorCode,
            message,
            detail,
            correlationId
        }, statusCode: statusCode);
    }
}

internal sealed class JwtOptions
{
    public string Issuer { get; init; } = "DaprFx.AuthService";
    public string Audience { get; init; } = "DaprFx.Services";
    public string SigningKey { get; init; } = "DaprFx.Dev.Secret.Key.ChangeMe";
}
