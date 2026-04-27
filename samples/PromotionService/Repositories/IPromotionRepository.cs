using PromotionService.Models;

namespace PromotionService.Repositories;

public interface IPromotionRepository
{
    Task<List<PromotionCampaign>> GetAllAsync(CancellationToken cancellationToken);
    Task SaveAllAsync(List<PromotionCampaign> campaigns, CancellationToken cancellationToken);
}
