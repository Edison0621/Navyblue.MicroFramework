using DaprFx.Operations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDaprClient();
builder.Services.AddOpenApi();
builder.Services.AddDaprFxOperationsDashboard();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Ok(new { service = "ProductService", status = "ok" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));

app.UseAuthorization();

app.MapControllers();
app.MapDaprFxOperationsDashboard();

app.Run();
