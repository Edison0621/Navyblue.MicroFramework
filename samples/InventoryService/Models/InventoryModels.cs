namespace InventoryService.Models;

public sealed record SetStockRequest(int Quantity);
public sealed record ReserveStockRequest(int Quantity, string? ReservationId = null, int? TtlMinutes = null);
public sealed record ReleaseStockRequest(int Quantity, string? ReservationId = null);
public sealed record InventoryStock(string ProductId, int Quantity, DateTimeOffset UpdatedAt);

public sealed class InventoryReservationLedger
{
    public const string ProductIndexStateKey = "inventory:reservation:index";
    public List<string> ProductIds { get; set; } = [];
}

public sealed class InventoryReservationBucket
{
    public string ProductId { get; set; } = string.Empty;
    public List<InventoryReservationEntry> Entries { get; set; } = [];
}

public sealed class InventoryReservationEntry
{
    public string ReservationId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Released { get; set; }
    public string? ReleaseReason { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
}
