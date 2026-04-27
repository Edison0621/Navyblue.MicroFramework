namespace PromotionService.Models;

public sealed record CreatePromotionRequest(
    string Code,
    string Name,
    string DiscountType,
    decimal DiscountValue,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    bool IsEnabled);

public sealed record ValidatePromotionRequest(string Code, decimal OrderAmount);

/// <summary>
/// Root JSON body for POST /api/promotions/validate. Dapr service invocation deserializes this type directly (not wrapped in ApiResponse).
/// </summary>
public sealed record PromotionValidationResult(
    bool Valid,
    string? Reason,
    string? Code,
    string? DiscountType,
    decimal DiscountValue,
    decimal OrderAmount,
    decimal DiscountAmount,
    decimal FinalAmount);

public sealed record PromotionCampaign(
    string Code,
    string Name,
    string DiscountType,
    decimal DiscountValue,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    bool IsEnabled,
    DateTimeOffset CreatedAt);
