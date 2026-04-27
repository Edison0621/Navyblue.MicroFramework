using System.Security.Claims;
using System.Security.Cryptography;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Models;
using UserService.Repositories;

namespace UserService.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IUserRepository userRepository) : ControllerBase
{
    private const string SeedUserId = "11111111-1111-1111-1111-111111111111";
    private const string SeedAdminId = "22222222-2222-2222-2222-222222222222";

    [HttpPost("seed")]
    public async Task<IActionResult> Seed(CancellationToken cancellationToken)
    {
        var ids = await userRepository.GetIdsAsync(cancellationToken);
        var demoId = Guid.Parse(SeedUserId);
        var demo = new UserRecord(demoId, "demo", "demo@example.com", Hash("demo123"), "active", ["user"], null);
        await userRepository.SaveAsync(demo, cancellationToken);
        ids.Add(demo.Id);
        await userRepository.SaveIdsAsync(ids, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "Seed user created.", demo.Id }, null));
    }

    [HttpPost("seed-admin")]
    public async Task<IActionResult> SeedAdmin(CancellationToken cancellationToken)
    {
        var ids = await userRepository.GetIdsAsync(cancellationToken);
        var adminId = Guid.Parse(SeedAdminId);
        var admin = new UserRecord(adminId, "admin", "admin@example.com", Hash("admin123"), "active", ["admin"], null);
        await userRepository.SaveAsync(admin, cancellationToken);
        ids.Add(admin.Id);
        await userRepository.SaveIdsAsync(ids, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "Seed admin created.", admin.Id }, null));
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] string? tag,
        [FromQuery] bool? blacklisted,
        CancellationToken cancellationToken)
    {
        var ids = await userRepository.GetIdsAsync(cancellationToken);
        var users = new List<UserView>();
        foreach (var id in ids)
        {
            var user = await userRepository.GetAsync(id, cancellationToken);
            if (user is not null)
            {
                users.Add(user.ToView());
            }
        }

        var query = new PageQuery(page, pageSize);
        IEnumerable<UserView> filtered = users;
        if (!string.IsNullOrWhiteSpace(q))
        {
            filtered = filtered.Where(x =>
                x.Username.Contains(q, StringComparison.OrdinalIgnoreCase)
                || x.Email.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            filtered = filtered.Where(x => x.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)));
        }

        if (blacklisted.HasValue)
        {
            filtered = filtered.Where(x => x.Blacklist.IsBlacklisted == blacklisted.Value);
        }

        var ordered = filtered.OrderBy(x => x.Username).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize).ToList();
        var data = new PagedResult<UserView>(items, query.SafePage, query.SafePageSize, total);
        return Ok(new ApiResponse<PagedResult<UserView>>(true, data, null));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetAsync(id, cancellationToken);
        return user is null
            ? NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "User not found.")))
            : Ok(new ApiResponse<UserView>(true, user.ToView(), null));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Upsert(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var current = await userRepository.GetAsync(id, cancellationToken)
            ?? new UserRecord(id, request.Username, request.Email, Hash("ChangeMe123!"), "active", ["user"], request.AvatarUrl);
        var updated = current with { Username = request.Username, Email = request.Email, AvatarUrl = request.AvatarUrl };
        await userRepository.SaveAsync(updated, cancellationToken);
        var ids = await userRepository.GetIdsAsync(cancellationToken);
        if (ids.Add(id))
        {
            await userRepository.SaveIdsAsync(ids, cancellationToken);
        }

        return Ok(new ApiResponse<UserView>(true, updated.ToView(), null));
    }

    [HttpPatch("{id:guid}/roles")]
    public async Task<IActionResult> UpdateRoles(Guid id, [FromBody] UpdateRolesRequest request, CancellationToken cancellationToken)
    {
        var current = await userRepository.GetAsync(id, cancellationToken);
        if (current is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "User not found.")));
        }

        var updated = current with { Roles = request.Roles };
        await userRepository.SaveAsync(updated, cancellationToken);
        return Ok(new ApiResponse<UserView>(true, updated.ToView(), null));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request, CancellationToken cancellationToken)
    {
        var current = await userRepository.GetAsync(id, cancellationToken);
        if (current is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "User not found.")));
        }

        var updated = current with { Status = request.Status };
        await userRepository.SaveAsync(updated, cancellationToken);
        return Ok(new ApiResponse<UserView>(true, updated.ToView(), null));
    }

    [Authorize(Roles = "admin")]
    [HttpPatch("{id:guid}/level")]
    public async Task<IActionResult> UpdateLevel(Guid id, [FromBody] UpdateUserLevelRequest request, CancellationToken cancellationToken)
    {
        var current = await userRepository.GetAsync(id, cancellationToken);
        if (current is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "User not found.")));
        }

        var updated = current with
        {
            Level = string.IsNullOrWhiteSpace(request.Level) ? current.Level : request.Level.Trim(),
            Points = request.Points ?? current.Points,
            GrowthValue = request.GrowthValue ?? current.GrowthValue
        };
        await userRepository.SaveAsync(updated, cancellationToken);
        await WriteAuditAsync("user.level.updated", "user", id.ToString(), "success", request.Note, cancellationToken);
        return Ok(new ApiResponse<UserView>(true, updated.ToView(), null));
    }

    [Authorize(Roles = "admin")]
    [HttpPatch("{id:guid}/tags")]
    public async Task<IActionResult> UpdateTags(Guid id, [FromBody] UpdateUserTagsRequest request, CancellationToken cancellationToken)
    {
        var current = await userRepository.GetAsync(id, cancellationToken);
        if (current is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "User not found.")));
        }

        var tags = (request.Tags ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var updated = current with { Tags = tags };
        await userRepository.SaveAsync(updated, cancellationToken);
        await WriteAuditAsync("user.tags.updated", "user", id.ToString(), "success", request.Note, cancellationToken);
        return Ok(new ApiResponse<UserView>(true, updated.ToView(), null));
    }

    [Authorize(Roles = "admin")]
    [HttpPatch("{id:guid}/blacklist")]
    public async Task<IActionResult> UpdateBlacklist(Guid id, [FromBody] UpdateUserBlacklistRequest request, CancellationToken cancellationToken)
    {
        var current = await userRepository.GetAsync(id, cancellationToken);
        if (current is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "User not found.")));
        }

        var blacklist = new UserBlacklistState(
            request.IsBlacklisted,
            request.Reason?.Trim(),
            request.ExpiresAt,
            DateTimeOffset.UtcNow);
        var updated = current with { Blacklist = blacklist };
        await userRepository.SaveAsync(updated, cancellationToken);
        await WriteAuditAsync("user.blacklist.updated", "user", id.ToString(), "success", request.Reason, cancellationToken);
        return Ok(new ApiResponse<UserView>(true, updated.ToView(), null));
    }

    [Authorize(Roles = "admin")]
    [HttpPatch("batch/level")]
    public async Task<IActionResult> BatchUpdateLevel([FromBody] BulkUserLevelRequest request, CancellationToken cancellationToken)
    {
        var results = new List<UserView>();
        foreach (var id in request.UserIds.Distinct())
        {
            var current = await userRepository.GetAsync(id, cancellationToken);
            if (current is null) continue;
            var updated = current with
            {
                Level = string.IsNullOrWhiteSpace(request.Level) ? current.Level : request.Level.Trim(),
                Points = request.Points ?? current.Points,
                GrowthValue = request.GrowthValue ?? current.GrowthValue
            };
            await userRepository.SaveAsync(updated, cancellationToken);
            results.Add(updated.ToView());
        }
        await WriteAuditAsync("user.level.bulk_updated", "user_batch", string.Join(',', request.UserIds), "success", request.Note, cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<UserView>>(true, results, null));
    }

    [Authorize(Roles = "admin")]
    [HttpPatch("batch/tags")]
    public async Task<IActionResult> BatchUpdateTags([FromBody] BulkUserTagsRequest request, CancellationToken cancellationToken)
    {
        var tags = (request.Tags ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var results = new List<UserView>();
        foreach (var id in request.UserIds.Distinct())
        {
            var current = await userRepository.GetAsync(id, cancellationToken);
            if (current is null) continue;
            var updated = current with { Tags = tags };
            await userRepository.SaveAsync(updated, cancellationToken);
            results.Add(updated.ToView());
        }
        await WriteAuditAsync("user.tags.bulk_updated", "user_batch", string.Join(',', request.UserIds), "success", request.Note, cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<UserView>>(true, results, null));
    }

    [Authorize(Roles = "admin")]
    [HttpPatch("batch/blacklist")]
    public async Task<IActionResult> BatchUpdateBlacklist([FromBody] BulkUserBlacklistRequest request, CancellationToken cancellationToken)
    {
        var results = new List<UserView>();
        foreach (var id in request.UserIds.Distinct())
        {
            var current = await userRepository.GetAsync(id, cancellationToken);
            if (current is null) continue;
            var blacklist = new UserBlacklistState(request.IsBlacklisted, request.Reason?.Trim(), request.ExpiresAt, DateTimeOffset.UtcNow);
            var updated = current with { Blacklist = blacklist };
            await userRepository.SaveAsync(updated, cancellationToken);
            results.Add(updated.ToView());
        }
        await WriteAuditAsync("user.blacklist.bulk_updated", "user_batch", string.Join(',', request.UserIds), "success", request.Reason, cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<UserView>>(true, results, null));
    }

    [Authorize]
    [HttpPost("me/close-request")]
    public async Task<IActionResult> SubmitClose([FromBody] SubmitCloseRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Missing user identity.")));
        }

        var current = await userRepository.GetAsync(userId, cancellationToken);
        if (current is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "User not found.")));
        }

        var close = new UserCloseRequestState(
            Status: "pending",
            Reason: request.Reason,
            Decision: null,
            DecisionNote: null,
            RequestedBy: userId.ToString(),
            RequestedAt: DateTimeOffset.UtcNow,
            ReviewedBy: null,
            ReviewedAt: null);
        var updated = current with { CloseRequest = close, Status = "close_pending" };
        await userRepository.SaveAsync(updated, cancellationToken);
        await WriteAuditAsync("user.close.requested", "user", userId.ToString(), "success", request.Reason, cancellationToken);
        return Ok(new ApiResponse<UserView>(true, updated.ToView(), null));
    }

    [Authorize(Roles = "admin")]
    [HttpPost("{id:guid}/close-review")]
    public async Task<IActionResult> ReviewClose(Guid id, [FromBody] ReviewCloseRequest request, CancellationToken cancellationToken)
    {
        var current = await userRepository.GetAsync(id, cancellationToken);
        if (current is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "User not found.")));
        }

        if (current.CloseRequest is null || !string.Equals(current.CloseRequest.Status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Conflict, "No pending close request.")));
        }

        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
        var approved = string.Equals(request.Decision, "approve", StringComparison.OrdinalIgnoreCase);
        var reviewed = current.CloseRequest with
        {
            Status = approved ? "approved" : "rejected",
            Decision = request.Decision,
            DecisionNote = request.Note,
            ReviewedBy = actor,
            ReviewedAt = DateTimeOffset.UtcNow
        };

        var updated = current with
        {
            CloseRequest = reviewed,
            Status = approved ? "closed" : "active"
        };
        await userRepository.SaveAsync(updated, cancellationToken);
        await WriteAuditAsync("user.close.reviewed", "user", id.ToString(), approved ? "approved" : "rejected", request.Note, cancellationToken);
        return Ok(new ApiResponse<UserView>(true, updated.ToView(), null));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Missing user identity.")));
        }

        var user = await userRepository.GetAsync(userId, cancellationToken);
        return user is null
            ? NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "User not found.")))
            : Ok(new ApiResponse<UserView>(true, user.ToView(), null));
    }

    [HttpPost("internal/register")]
    public async Task<IActionResult> RegisterInternal([FromBody] InternalRegisterRequest request, CancellationToken cancellationToken)
    {
        var ids = await userRepository.GetIdsAsync(cancellationToken);
        foreach (var userId in ids)
        {
            var existing = await userRepository.GetAsync(userId, cancellationToken);
            if (existing is null)
            {
                continue;
            }

            if (existing.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase)
                || existing.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Conflict, "Username or email already exists.")));
            }
        }

        var user = new UserRecord(Guid.NewGuid(), request.Username, request.Email, Hash(request.Password), "active", ["user"], null);
        await userRepository.SaveAsync(user, cancellationToken);
        ids.Add(user.Id);
        await userRepository.SaveIdsAsync(ids, cancellationToken);
        return Ok(new ApiResponse<InternalAuthUser>(true, ToInternalAuthUser(user), null));
    }

    [HttpPost("internal/verify")]
    public async Task<IActionResult> VerifyInternal([FromBody] InternalVerifyRequest request, CancellationToken cancellationToken)
    {
        var ids = await userRepository.GetIdsAsync(cancellationToken);
        foreach (var userId in ids)
        {
            var existing = await userRepository.GetAsync(userId, cancellationToken);
            if (existing is null)
            {
                continue;
            }

            var accountMatched =
                existing.Username.Equals(request.Account, StringComparison.OrdinalIgnoreCase)
                || existing.Email.Equals(request.Account, StringComparison.OrdinalIgnoreCase);
            if (!accountMatched)
            {
                continue;
            }

            if (!Verify(request.Password, existing.PasswordHash))
            {
                return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Invalid account or password.")));
            }

            return Ok(new ApiResponse<InternalAuthUser>(true, ToInternalAuthUser(existing), null));
        }

        return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Invalid account or password.")));
    }

    [HttpGet("internal/{id:guid}")]
    public async Task<IActionResult> GetInternal(Guid id, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetAsync(id, cancellationToken);
        return user is null
            ? NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "User not found.")))
            : Ok(new ApiResponse<InternalAuthUser>(true, ToInternalAuthUser(user), null));
    }

    private static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[0]);
        var expected = Convert.FromBase64String(parts[1]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }

    private async Task WriteAuditAsync(string action, string resourceType, string resourceId, string result, string? detail, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient();
            var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            var payload = new
            {
                actorId = actor,
                action,
                resourceType,
                resourceId,
                result,
                detail
            };
            await client.PostAsJsonAsync("http://auditservice:8080/api/audit/events", payload, cancellationToken);
        }
        catch
        {
            // keep user workflow successful even if audit service unavailable
        }
    }

    private static InternalAuthUser ToInternalAuthUser(UserRecord user)
    {
        var isBlacklisted = user.Blacklist?.IsBlacklisted == true
            && (user.Blacklist.ExpiresAt is null || user.Blacklist.ExpiresAt > DateTimeOffset.UtcNow);
        return new InternalAuthUser(
            user.Id,
            user.Username,
            user.Email,
            user.Roles,
            user.Status,
            isBlacklisted,
            user.CloseRequest?.Status);
    }
}
