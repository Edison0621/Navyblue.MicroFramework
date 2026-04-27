using InventoryService.Models;

namespace InventoryService.Repositories;

public interface IInventoryRepository
{
    Task<InventoryStock?> GetAsync(string productId, CancellationToken cancellationToken);
    Task<InventoryStock> SetAsync(string productId, int quantity, CancellationToken cancellationToken);
}
