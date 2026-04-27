namespace OrderService.Models;

public sealed record PromotionValidationRequest(string Code, decimal OrderAmount);

public sealed record PromotionValidationResult(
    bool Valid,
    string? Reason,
    string? Code,
    string? DiscountType,
    decimal DiscountValue,
    decimal OrderAmount,
    decimal DiscountAmount,
    decimal FinalAmount);
