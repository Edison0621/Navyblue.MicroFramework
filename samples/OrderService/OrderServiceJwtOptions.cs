namespace OrderService;

internal sealed class OrderServiceJwtOptions
{
    public string Issuer { get; init; } = "DaprFx.AuthService";
    public string Audience { get; init; } = "DaprFx.Services";
    public string SigningKey { get; init; } = "DaprFx.Dev.Secret.Key.ChangeMe.2026!";
}
