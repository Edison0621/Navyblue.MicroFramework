using InventoryService.Models;

namespace InventoryService.Repositories;

public interface IInventoryRepository
{
    Task<InventoryStock?> GetAsync(string productId, CancellationToken cancellationToken);
    Task<InventoryStock> SetAsync(string productId, int quantity, CancellationToken cancellationToken);
    Task<InventoryReservationBucket> GetReservationBucketAsync(string productId, CancellationToken cancellationToken);
    Task SaveReservationBucketAsync(string productId, InventoryReservationBucket bucket, CancellationToken cancellationToken);
    Task<InventoryReservationLedger> GetReservationLedgerAsync(CancellationToken cancellationToken);
    Task SaveReservationLedgerAsync(InventoryReservationLedger ledger, CancellationToken cancellationToken);
}
