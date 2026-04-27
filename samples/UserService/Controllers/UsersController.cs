using System.Security.Cryptography;
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
    public async Task<IActionResult> GetUsers([FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
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
        var ordered = users.OrderBy(x => x.Username).ToList();
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
        return Ok(new ApiResponse<InternalAuthUser>(true, new InternalAuthUser(user.Id, user.Username, user.Email, user.Roles, user.Status), null));
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

            return Ok(new ApiResponse<InternalAuthUser>(true, new InternalAuthUser(existing.Id, existing.Username, existing.Email, existing.Roles, existing.Status), null));
        }

        return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Invalid account or password.")));
    }

    [HttpGet("internal/{id:guid}")]
    public async Task<IActionResult> GetInternal(Guid id, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetAsync(id, cancellationToken);
        return user is null
            ? NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "User not found.")))
            : Ok(new ApiResponse<InternalAuthUser>(true, new InternalAuthUser(user.Id, user.Username, user.Email, user.Roles, user.Status), null));
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
}
