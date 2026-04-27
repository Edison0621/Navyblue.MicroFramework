using DaprFx.Core;
using OrderService.Models;

namespace OrderService.Abstractions;

public interface IPromotionService
{
    [DaprInvoke("/api/promotions/validate")]
    Task<PromotionValidationResult?> ValidateAsync(PromotionValidationRequest request);
}
