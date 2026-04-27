using Microsoft.AspNetCore.Mvc;
using PromotionService.Models;
using PromotionService.Repositories;

namespace PromotionService.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PromotionsController : ControllerBase
{
    private readonly IPromotionRepository _promotionRepository;

    public PromotionsController(IPromotionRepository promotionRepository)
    {
        _promotionRepository = promotionRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetPromotions([FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var query = new PageQuery(page, pageSize);
        var campaigns = await _promotionRepository.GetAllAsync(cancellationToken);
        var ordered = campaigns.OrderByDescending(x => x.CreatedAt).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize).ToList();
        var data = new PagedResult<PromotionCampaign>(items, query.SafePage, query.SafePageSize, total);
        return Ok(new ApiResponse<PagedResult<PromotionCampaign>>(true, data, null));
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetPromotionByCode(string code, CancellationToken cancellationToken)
    {
        var campaigns = await _promotionRepository.GetAllAsync(cancellationToken);
        var matched = campaigns.FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        return matched is null
            ? NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Promotion not found.")))
            : Ok(new ApiResponse<PromotionCampaign>(true, matched, null));
    }

    [HttpPost]
    public async Task<IActionResult> CreatePromotion([FromBody] CreatePromotionRequest request, CancellationToken cancellationToken)
    {
        var campaigns = await _promotionRepository.GetAllAsync(cancellationToken);
        if (campaigns.Any(x => x.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Conflict, "Promotion code already exists.")));
        }

        var created = new PromotionCampaign(
            request.Code,
            request.Name,
            request.DiscountType,
            request.DiscountValue,
            request.StartAt,
            request.EndAt,
            request.IsEnabled,
            DateTimeOffset.UtcNow);
        campaigns.Add(created);
        await _promotionRepository.SaveAllAsync(campaigns, cancellationToken);
        return Created($"/api/promotions/{created.Code}", new ApiResponse<PromotionCampaign>(true, created, null));
    }

    [HttpPost("validate")]
    public async Task<IActionResult> ValidatePromotion([FromBody] ValidatePromotionRequest request, CancellationToken cancellationToken)
    {
        var campaigns = await _promotionRepository.GetAllAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var campaign = campaigns.FirstOrDefault(x => x.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase));
        if (campaign is null)
        {
            return Ok(new PromotionValidationResult(false, "not_found", null, null, 0, request.OrderAmount, 0, request.OrderAmount));
        }

        if (!campaign.IsEnabled)
        {
            return Ok(new PromotionValidationResult(false, "disabled", campaign.Code, campaign.DiscountType, campaign.DiscountValue, request.OrderAmount, 0, request.OrderAmount));
        }

        if (campaign.StartAt > now || campaign.EndAt < now)
        {
            return Ok(new PromotionValidationResult(false, "out_of_window", campaign.Code, campaign.DiscountType, campaign.DiscountValue, request.OrderAmount, 0, request.OrderAmount));
        }

        var discountAmount = campaign.DiscountType.Equals("percentage", StringComparison.OrdinalIgnoreCase)
            ? Math.Round(request.OrderAmount * (campaign.DiscountValue / 100m), 2)
            : Math.Min(campaign.DiscountValue, request.OrderAmount);
        var finalAmount = Math.Max(0, request.OrderAmount - discountAmount);
        return Ok(new PromotionValidationResult(
            true,
            null,
            campaign.Code,
            campaign.DiscountType,
            campaign.DiscountValue,
            request.OrderAmount,
            discountAmount,
            finalAmount));
    }
}
