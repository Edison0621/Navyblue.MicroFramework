namespace OrderService.Models;

public sealed record UserAuthProfileDto(
    Guid Id,
    string Username,
    string Email,
    string[] Roles,
    string Status,
    bool IsBlacklisted,
    string? CloseRequestStatus);
