namespace UserService.Models;

public sealed record BuyerProfileView(Guid Id, string Username, string Email, string? AvatarUrl, string? Gender, DateOnly? Birthday);
public sealed record UpdateBuyerProfileRequest(string Username, string Email, string? AvatarUrl, string? Gender, DateOnly? Birthday);

public sealed record BuyerLoyaltyView(string Level, int Points, int GrowthValue);
public sealed record UpdateBuyerLoyaltyRequest(string Level, int Points, int GrowthValue);

public sealed record BuyerCouponView(string Id, string Title, decimal Amount, string Status, DateTimeOffset ExpireAt);
public sealed record UpsertBuyerCouponRequest(string Id, string Title, decimal Amount, string Status, DateTimeOffset ExpireAt);

public sealed record BuyerInvoiceTitleView(string Id, string Type, string Name, string? TaxNo, bool IsDefault);
public sealed record UpsertBuyerInvoiceTitleRequest(string Id, string Type, string Name, string? TaxNo, bool IsDefault);

public sealed record BuyerFavoriteView(string Id, string Type, string TargetId, string Name);
public sealed record UpsertBuyerFavoriteRequest(string Id, string Type, string TargetId, string Name);

public sealed record BuyerFootprintView(string Id, string ProductId, string Name, DateTimeOffset VisitedAt);
public sealed record UpsertBuyerFootprintRequest(string Id, string ProductId, string Name, DateTimeOffset VisitedAt);

public sealed record UpsertRecentSearchRequest(string Term);

public sealed record BuyerReviewView(string OrderId, string SubOrderId, int Rating, string Content, DateTimeOffset CreatedAt);
public sealed record CreateBuyerReviewRequest(string OrderId, string SubOrderId, int Rating, string Content);
