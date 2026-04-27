using System.Text;
using DaprFx.Core;
using OrderService;
using DaprFx.Hosting;
using DaprFx.Operations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OrderService.Abstractions;
using OrderService.Models;
using OrderService.Services;

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<OrderServiceJwtOptions>() ?? new OrderServiceJwtOptions();
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
builder.Services.AddSingleton<EventAuditStore>();
builder.Services.AddSingleton(builder.Configuration.GetSection("PaymentGateway").Get<PaymentGatewayOptions>() ?? new PaymentGatewayOptions());
builder.Services.AddSingleton<IPaymentGateway, SimulatedPaymentGateway>();
builder.Services.AddSingleton(builder.Configuration.GetSection("ShipmentTracking").Get<ShipmentTrackingOptions>() ?? new ShipmentTrackingOptions());
builder.Services.AddSingleton<IShipmentTrackingProvider, MockShipmentTrackingProvider>();
builder.Services.AddSingleton(builder.Configuration.GetSection("PaymentCallback").Get<PaymentCallbackOptions>() ?? new PaymentCallbackOptions());
builder.Services.AddDaprFxOperationsDashboard();
builder.Services.AddOpsDashboardContributor<OrderEventsDashboardContributor>();
builder.AddDaprMicroFramework(options =>
{
    options.UseEventBus = true;
    options.PubSubName = "orderpubsub";
    options.ConfigStore = "appconfig";
    options.UseCryptography = true;
    options.StateStoreName = "statestore";
    options.OutboxStateKey = "order:outbox";
    options.OutboxDeadLetterStateKey = "order:outbox:dead";
    options.OutboxMaxRetryCount = 5;
    options.OutboxBaseDelaySeconds = 2;
    options.IdempotencyStateKeyPrefix = "order:idempotency:";
    options.IdempotencyTtlMinutes = 24 * 60;
    options.OtlpEndpoint = builder.Configuration["Demo:OtlpEndpoint"] ?? "http://localhost:4317";
    options.TelemetryServiceName = "OrderService";
    options.TelemetryEnvironment = builder.Environment.EnvironmentName;
    options.InvocationMaxRetries = 3;
    options.InvocationTimeoutSeconds = 5;
    options.CircuitBreakerFailureThreshold = 3;
    options.CircuitBreakerOpenSeconds = 20;
    options.AddServiceClient(typeof(IProductService), client =>
    {
        client.LoadBalancingStrategy = LoadBalancingStrategy.Sticky;
        client.InvocationTimeoutSeconds = 3;
        client.InvocationMaxRetries = 2;
    }, "productservice", "productservice-canary");
    options.AddServiceClient(typeof(IInventoryService), client =>
    {
        client.InvocationTimeoutSeconds = 3;
        client.InvocationMaxRetries = 1;
    }, "inventoryservice");
    options.AddServiceClient(typeof(IAuditService), client =>
    {
        client.InvocationTimeoutSeconds = 3;
        client.InvocationMaxRetries = 1;
    }, "auditservice");
    options.AddServiceClient(typeof(IPromotionService), client =>
    {
        client.InvocationTimeoutSeconds = 3;
        client.InvocationMaxRetries = 1;
    }, "promotionservice");
    options.AddServiceClient(typeof(ICatalogService), client =>
    {
        client.InvocationTimeoutSeconds = 3;
        client.InvocationMaxRetries = 2;
    }, "catalogservice");
    options.AddServiceClient(typeof(IUserService), client =>
    {
        client.InvocationTimeoutSeconds = 3;
        client.InvocationMaxRetries = 1;
    }, "userservice");
    options.AddStateStore(typeof(Order), "statestore");
    options.AddStateStore(typeof(ShoppingCart), "statestore");
    options.AddStateStore(typeof(UserOrderIndex), "statestore");
    options.AddStateStore(typeof(ShopOrderIndex), "statestore");
    options.AddStateStore(typeof(PaymentPendingIndex), "statestore");
    options.AddStateStore(typeof(OrderPayIdempotencyRecord), "statestore");
    options.AddStateStore(typeof(OrderRefundIdempotencyRecord), "statestore");
    options.AddStateStore(typeof(RefundLedgerIndex), "statestore");
});

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseDaprFx();
app.MapDaprFxOperationsDashboard();
app.Run();
