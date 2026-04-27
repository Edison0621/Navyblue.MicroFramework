using Microsoft.AspNetCore.Mvc;
using UserService.Models;
using UserService.Repositories;

namespace UserService.Controllers;

/// <summary>
/// Trusted in-cluster lookup for OrderService checkout (no end-user auth).
/// </summary>
[ApiController]
[Route("api/users/internal")]
public sealed class InternalUserAddressesController(IAddressBookRepository addressBookRepository) : ControllerBase
{
    private readonly IAddressBookRepository _addressBookRepository = addressBookRepository;

    [HttpGet("{userId:guid}/addresses/{addressId:guid}")]
    public async Task<IActionResult> GetAddress(Guid userId, Guid addressId, CancellationToken cancellationToken)
    {
        var list = await _addressBookRepository.ListAsync(userId, cancellationToken);
        var match = list.FirstOrDefault(a => a.Id == addressId);
        if (match is null)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Address not found.")));
        }

        var dto = new UserAddressView(match.Id, match.ReceiverName, match.Phone, match.Region, match.Detail, match.IsDefault);
        return Ok(new ApiResponse<UserAddressView>(true, dto, null));
    }
}
