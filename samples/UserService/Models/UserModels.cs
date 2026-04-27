namespace UserService.Models;

public sealed record UpdateUserRequest(string Username, string Email, string? AvatarUrl);
public sealed record UpdateRolesRequest(string[] Roles);
public sealed record UpdateStatusRequest(string Status);
public sealed record InternalRegisterRequest(string Username, string Email, string Password);
public sealed record InternalVerifyRequest(string Account, string Password);
public sealed record InternalAuthUser(Guid Id, string Username, string Email, string[] Roles, string Status);

public sealed record UserView(Guid Id, string Username, string Email, string Status, string[] Roles, string? AvatarUrl);
public sealed record UserRecord(Guid Id, string Username, string Email, string PasswordHash, string Status, string[] Roles, string? AvatarUrl)
{
    public UserView ToView() => new(Id, Username, Email, Status, Roles, AvatarUrl);
}
