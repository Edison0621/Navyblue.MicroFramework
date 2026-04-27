namespace UserService.Models;

public sealed record UpdateUserRequest(string Username, string Email, string? AvatarUrl);
public sealed record UpdateRolesRequest(string[] Roles);
public sealed record UpdateStatusRequest(string Status);
public sealed record UpdateUserLevelRequest(string Level, int? Points = null, int? GrowthValue = null, string? Note = null);
public sealed record UpdateUserTagsRequest(string[] Tags, string? Note = null);
public sealed record UpdateUserBlacklistRequest(bool IsBlacklisted, string? Reason = null, DateTimeOffset? ExpiresAt = null);
public sealed record BulkUserLevelRequest(Guid[] UserIds, string Level, int? Points = null, int? GrowthValue = null, string? Note = null);
public sealed record BulkUserTagsRequest(Guid[] UserIds, string[] Tags, string? Note = null);
public sealed record BulkUserBlacklistRequest(Guid[] UserIds, bool IsBlacklisted, string? Reason = null, DateTimeOffset? ExpiresAt = null);
public sealed record SubmitCloseRequest(string Reason);
public sealed record ReviewCloseRequest(string Decision, string? Note = null);
public sealed record InternalRegisterRequest(string Username, string Email, string Password);
public sealed record InternalVerifyRequest(string Account, string Password);
public sealed record InternalAuthUser(Guid Id, string Username, string Email, string[] Roles, string Status, bool IsBlacklisted = false, string? CloseRequestStatus = null);

public sealed record UserBlacklistState(bool IsBlacklisted, string? Reason, DateTimeOffset? ExpiresAt, DateTimeOffset? UpdatedAt);
public sealed record UserCloseRequestState(
    string Status,
    string? Reason,
    string? Decision,
    string? DecisionNote,
    string? RequestedBy,
    DateTimeOffset? RequestedAt,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt);

public sealed record UserView(
    Guid Id,
    string Username,
    string Email,
    string Status,
    string[] Roles,
    string? AvatarUrl,
    string Level,
    int Points,
    int GrowthValue,
    string[] Tags,
    UserBlacklistState Blacklist,
    UserCloseRequestState? CloseRequest);

public sealed record UserRecord(
    Guid Id,
    string Username,
    string Email,
    string PasswordHash,
    string Status,
    string[] Roles,
    string? AvatarUrl,
    string Level = "normal",
    int Points = 0,
    int GrowthValue = 0,
    string[]? Tags = null,
    UserBlacklistState? Blacklist = null,
    UserCloseRequestState? CloseRequest = null)
{
    public UserView ToView() => new(
        Id,
        Username,
        Email,
        Status,
        Roles,
        AvatarUrl,
        Level,
        Points,
        GrowthValue,
        Tags ?? [],
        Blacklist ?? new UserBlacklistState(false, null, null, null),
        CloseRequest);
}
