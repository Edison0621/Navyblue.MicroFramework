using System.Security.Claims;
using Dapr.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Models;
using UserService.Repositories;

namespace UserService.Controllers;

[ApiController]
[Route("api/users/me")]
[Authorize]
public sealed class BuyerDomainController(DaprClient daprClient, IUserRepository userRepository) : ControllerBase
{
    private const string StateStoreName = "statestore";

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Missing user identity.")));
        }

        var user = await userRepository.GetAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "User not found.")));
        }

        var profile = new BuyerProfileView(user.Id, user.Username, user.Email, user.AvatarUrl, null, null);
        return Ok(new ApiResponse<BuyerProfileView>(true, profile, null));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpsertProfile([FromBody] UpdateBuyerProfileRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Missing user identity.")));
        }

        var user = await userRepository.GetAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "User not found.")));
        }

        var updated = user with { Username = request.Username.Trim(), Email = request.Email.Trim(), AvatarUrl = request.AvatarUrl };
        await userRepository.SaveAsync(updated, cancellationToken);
        var profile = new BuyerProfileView(updated.Id, updated.Username, updated.Email, updated.AvatarUrl, request.Gender, request.Birthday);
        await SaveStateAsync(BuildProfileExtKey(userId), new { request.Gender, request.Birthday }, cancellationToken);
        return Ok(new ApiResponse<BuyerProfileView>(true, profile, null));
    }

    [HttpGet("loyalty")]
    public async Task<IActionResult> GetLoyalty(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Missing user identity.")));
        var key = BuildKey(userId, "loyalty");
        var state = await daprClient.GetStateAsync<BuyerLoyaltyView>(StateStoreName, key, cancellationToken: cancellationToken)
            ?? new BuyerLoyaltyView("normal", 0, 0);
        return Ok(new ApiResponse<BuyerLoyaltyView>(true, state, null));
    }

    [HttpPut("loyalty")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpsertLoyalty([FromBody] UpdateBuyerLoyaltyRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Missing user identity.")));
        var state = new BuyerLoyaltyView(request.Level, request.Points, request.GrowthValue);
        await SaveStateAsync(BuildKey(userId, "loyalty"), state, cancellationToken);
        return Ok(new ApiResponse<BuyerLoyaltyView>(true, state, null));
    }

    [HttpGet("coupons")]
    public async Task<IActionResult> ListCoupons(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyList<BuyerCouponView>>(true, await GetListAsync<BuyerCouponView>("coupons", cancellationToken), null));

    [HttpPost("coupons")]
    public async Task<IActionResult> UpsertCoupon([FromBody] UpsertBuyerCouponRequest request, CancellationToken cancellationToken)
    {
        var list = (await GetListAsync<BuyerCouponView>("coupons", cancellationToken)).ToList();
        list.RemoveAll(x => x.Id == request.Id);
        var row = new BuyerCouponView(request.Id, request.Title, request.Amount, request.Status, request.ExpireAt);
        list.Insert(0, row);
        await SaveListAsync("coupons", list, cancellationToken);
        return Ok(new ApiResponse<BuyerCouponView>(true, row, null));
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> ListInvoices(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyList<BuyerInvoiceTitleView>>(true, await GetListAsync<BuyerInvoiceTitleView>("invoices", cancellationToken), null));

    [HttpPost("invoices")]
    public async Task<IActionResult> UpsertInvoice([FromBody] UpsertBuyerInvoiceTitleRequest request, CancellationToken cancellationToken)
    {
        var list = (await GetListAsync<BuyerInvoiceTitleView>("invoices", cancellationToken)).ToList();
        list.RemoveAll(x => x.Id == request.Id);
        var row = new BuyerInvoiceTitleView(request.Id, request.Type, request.Name, request.TaxNo, request.IsDefault);
        list.Insert(0, row);
        await SaveListAsync("invoices", list, cancellationToken);
        return Ok(new ApiResponse<BuyerInvoiceTitleView>(true, row, null));
    }

    [HttpGet("favorites")]
    public async Task<IActionResult> ListFavorites(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyList<BuyerFavoriteView>>(true, await GetListAsync<BuyerFavoriteView>("favorites", cancellationToken), null));

    [HttpPost("favorites")]
    public async Task<IActionResult> UpsertFavorite([FromBody] UpsertBuyerFavoriteRequest request, CancellationToken cancellationToken)
    {
        var list = (await GetListAsync<BuyerFavoriteView>("favorites", cancellationToken)).ToList();
        list.RemoveAll(x => x.Type == request.Type && x.TargetId == request.TargetId);
        var row = new BuyerFavoriteView(request.Id, request.Type, request.TargetId, request.Name);
        list.Insert(0, row);
        await SaveListAsync("favorites", list, cancellationToken);
        return Ok(new ApiResponse<BuyerFavoriteView>(true, row, null));
    }

    [HttpGet("footprints")]
    public async Task<IActionResult> ListFootprints(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyList<BuyerFootprintView>>(true, await GetListAsync<BuyerFootprintView>("footprints", cancellationToken), null));

    [HttpPost("footprints")]
    public async Task<IActionResult> UpsertFootprint([FromBody] UpsertBuyerFootprintRequest request, CancellationToken cancellationToken)
    {
        var list = (await GetListAsync<BuyerFootprintView>("footprints", cancellationToken)).ToList();
        list.RemoveAll(x => x.ProductId == request.ProductId);
        var row = new BuyerFootprintView(request.Id, request.ProductId, request.Name, request.VisitedAt);
        list.Insert(0, row);
        await SaveListAsync("footprints", list, cancellationToken);
        return Ok(new ApiResponse<BuyerFootprintView>(true, row, null));
    }

    [HttpGet("searches")]
    public async Task<IActionResult> ListSearches(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyList<string>>(true, await GetListAsync<string>("searches", cancellationToken), null));

    [HttpPost("searches")]
    public async Task<IActionResult> UpsertSearch([FromBody] UpsertRecentSearchRequest request, CancellationToken cancellationToken)
    {
        var list = (await GetListAsync<string>("searches", cancellationToken)).ToList();
        list.RemoveAll(x => string.Equals(x, request.Term, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, request.Term);
        await SaveListAsync("searches", list.Take(20).ToList(), cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<string>>(true, list.Take(20).ToList(), null));
    }

    [HttpGet("reviews")]
    public async Task<IActionResult> ListReviews(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyList<BuyerReviewView>>(true, await GetListAsync<BuyerReviewView>("reviews", cancellationToken), null));

    [HttpPost("reviews")]
    public async Task<IActionResult> CreateReview([FromBody] CreateBuyerReviewRequest request, CancellationToken cancellationToken)
    {
        var list = (await GetListAsync<BuyerReviewView>("reviews", cancellationToken)).ToList();
        var row = new BuyerReviewView(request.OrderId, request.SubOrderId, request.Rating, request.Content, DateTimeOffset.UtcNow);
        list.Insert(0, row);
        await SaveListAsync("reviews", list, cancellationToken);
        return Ok(new ApiResponse<BuyerReviewView>(true, row, null));
    }

    private bool TryGetUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string suffix, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return [];
        var key = BuildKey(userId, suffix);
        return await daprClient.GetStateAsync<List<T>>(StateStoreName, key, cancellationToken: cancellationToken) ?? [];
    }

    private async Task SaveListAsync<T>(string suffix, IReadOnlyList<T> values, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return;
        await daprClient.SaveStateAsync(StateStoreName, BuildKey(userId, suffix), values, cancellationToken: cancellationToken);
    }

    private async Task SaveStateAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        await daprClient.SaveStateAsync(StateStoreName, key, value, cancellationToken: cancellationToken);
    }

    private static string BuildKey(Guid userId, string suffix) => $"user:{userId:N}:{suffix}";
    private static string BuildProfileExtKey(Guid userId) => BuildKey(userId, "profile-ext");
}
