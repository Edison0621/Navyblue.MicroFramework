using Dapr.Client;
using PromotionService.Models;

namespace PromotionService.Repositories;

public sealed class DaprPromotionRepository(DaprClient daprClient) : IPromotionRepository
{
    private const string StateStoreName = "statestore";
    private const string PromotionsKey = "promotion:campaigns";

    public async Task<List<PromotionCampaign>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await daprClient.GetStateAsync<List<PromotionCampaign>>(StateStoreName, PromotionsKey, cancellationToken: cancellationToken) ?? [];
    }

    public Task SaveAllAsync(List<PromotionCampaign> campaigns, CancellationToken cancellationToken)
    {
        return daprClient.SaveStateAsync(StateStoreName, PromotionsKey, campaigns, cancellationToken: cancellationToken);
    }
}
