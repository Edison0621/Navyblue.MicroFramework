using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using AuthService.Models;
using AuthService.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IHttpClientFactory httpClientFactory,
    IRefreshTokenRepository refreshTokenRepository,
    JwtOptions jwtOptions) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        var response = await client.PostAsJsonAsync("http://userservice:8080/api/users/internal/register", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Conflict, "Username or email already exists.")));
        }

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.UpstreamError, "User service returned an unexpected status.")));
        }

        var user = await response.Content.ReadFromJsonAsync<AuthUserResponse>(cancellationToken: cancellationToken);
        if (user is null)
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidResponse, "Failed to register user.")));
        }

        return Ok(new ApiResponse<object>(true, new { userId = user.Id, user.Username, user.Email, message = "Register success." }, null));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        var verifyResponse = await client.PostAsJsonAsync("http://userservice:8080/api/users/internal/verify", request, cancellationToken);
        if (verifyResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Invalid account or password.")));
        }

        if (!verifyResponse.IsSuccessStatusCode)
        {
            return StatusCode((int)verifyResponse.StatusCode, new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.UpstreamError, "User service returned an unexpected status.")));
        }

        var user = await verifyResponse.Content.ReadFromJsonAsync<AuthUserResponse>(cancellationToken: cancellationToken);
        if (user is null)
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Invalid account or password.")));
        }

        var refreshToken = $"refresh-{Guid.NewGuid():N}";
        await refreshTokenRepository.SaveAsync(refreshToken, user.Id, cancellationToken);
        return Ok(new ApiResponse<object>(true, new
        {
            accessToken = global::JwtTokenIssuer.Issue(user, jwtOptions),
            refreshToken,
            tokenType = "Bearer",
            expiresIn = 3600
        }, null));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var userId = await refreshTokenRepository.GetUserIdAsync(request.RefreshToken, cancellationToken);
        if (userId is null)
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Invalid refresh token.")));
        }

        var client = httpClientFactory.CreateClient();
        var response = await client.GetAsync($"http://userservice:8080/api/users/internal/{userId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Failed to load user from refresh token.")));
        }

        var user = await response.Content.ReadFromJsonAsync<AuthUserResponse>(cancellationToken: cancellationToken);
        if (user is null)
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Failed to load user from refresh token.")));
        }

        var nextRefresh = $"refresh-{Guid.NewGuid():N}";
        await refreshTokenRepository.DeleteAsync(request.RefreshToken, cancellationToken);
        await refreshTokenRepository.SaveAsync(nextRefresh, user.Id, cancellationToken);
        return Ok(new ApiResponse<object>(true, new
        {
            accessToken = global::JwtTokenIssuer.Issue(user, jwtOptions),
            refreshToken = nextRefresh,
            tokenType = "Bearer",
            expiresIn = 3600
        }, null));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        await refreshTokenRepository.DeleteAsync(request.RefreshToken, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { message = "Logged out." }, null));
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new ApiResponse<object>(true, new
        {
            userId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            username = User.Identity?.Name,
            roles = User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray()
        }, null));
    }
}
