namespace AuthService.Models;

public sealed record RegisterRequest(string Username, string Email, string Password);
public sealed record LoginRequest(string Account, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record AuthUserResponse(Guid Id, string Username, string Email, string[] Roles, string Status);

public sealed class JwtOptions
{
    public string Issuer { get; init; } = "DaprFx.AuthService";
    public string Audience { get; init; } = "DaprFx.Services";
    public string SigningKey { get; init; } = "DaprFx.Dev.Secret.Key.ChangeMe";
    public int ExpiresMinutes { get; init; } = 60;
}
