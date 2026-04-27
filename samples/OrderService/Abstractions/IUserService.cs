using DaprFx.Core;
using OrderService.Models;

namespace OrderService.Abstractions;

public interface IUserService
{
    [DaprInvoke("/api/users/internal/{userId}/addresses/{addressId}")]
    Task<ApiResponse<UserAddressSnapshotDto>?> GetUserAddressAsync(string userId, string addressId);

    [DaprInvoke("/api/users/internal/{id}")]
    Task<ApiResponse<UserAuthProfileDto>?> GetUserInternalAsync(string id);
}
