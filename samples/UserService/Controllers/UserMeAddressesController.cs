using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Models;
using UserService.Repositories;

namespace UserService.Controllers;

[ApiController]
[Route("api/users/me/addresses")]
[Authorize]
public sealed class UserMeAddressesController(IAddressBookRepository addressBookRepository) : ControllerBase
{
    private readonly IAddressBookRepository _addressBookRepository = addressBookRepository;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Missing user identity.")));
        }

        var list = await _addressBookRepository.ListAsync(userId, cancellationToken);
        var views = list.Select(a => new UserAddressView(a.Id, a.ReceiverName, a.Phone, a.Region, a.Detail, a.IsDefault)).ToList();
        return Ok(new ApiResponse<IReadOnlyList<UserAddressView>>(true, views, null));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserAddressRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Missing user identity.")));
        }

        if (string.IsNullOrWhiteSpace(request.ReceiverName) || string.IsNullOrWhiteSpace(request.Detail))
        {
            return BadRequest(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.InvalidRequest, "ReceiverName and Detail are required.")));
        }

        var addresses = (await _addressBookRepository.ListAsync(userId, cancellationToken)).ToList();
        var entry = new UserAddress
        {
            ReceiverName = request.ReceiverName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? string.Empty : request.Phone.Trim(),
            Region = string.IsNullOrWhiteSpace(request.Region) ? string.Empty : request.Region.Trim(),
            Detail = request.Detail.Trim(),
            IsDefault = request.IsDefault
        };

        if (entry.IsDefault || addresses.Count == 0)
        {
            foreach (var a in addresses)
            {
                a.IsDefault = false;
            }

            entry.IsDefault = true;
        }

        addresses.Add(entry);
        await _addressBookRepository.SaveAllAsync(userId, addresses, cancellationToken);
        return Ok(new ApiResponse<UserAddressView>(
            true,
            new UserAddressView(entry.Id, entry.ReceiverName, entry.Phone, entry.Region, entry.Detail, entry.IsDefault),
            null));
    }

    [HttpPatch("{addressId:guid}")]
    public async Task<IActionResult> Update(Guid addressId, [FromBody] UpdateUserAddressRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Missing user identity.")));
        }

        var addresses = (await _addressBookRepository.ListAsync(userId, cancellationToken)).ToList();
        var idx = addresses.FindIndex(a => a.Id == addressId);
        if (idx < 0)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Address not found.")));
        }

        var a = addresses[idx];
        if (!string.IsNullOrWhiteSpace(request.ReceiverName))
        {
            a.ReceiverName = request.ReceiverName.Trim();
        }

        if (request.Phone is not null)
        {
            a.Phone = string.IsNullOrWhiteSpace(request.Phone) ? string.Empty : request.Phone.Trim();
        }

        if (request.Region is not null)
        {
            a.Region = string.IsNullOrWhiteSpace(request.Region) ? string.Empty : request.Region.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Detail))
        {
            a.Detail = request.Detail.Trim();
        }

        if (request.IsDefault == true)
        {
            foreach (var x in addresses)
            {
                x.IsDefault = false;
            }

            a.IsDefault = true;
        }

        await _addressBookRepository.SaveAllAsync(userId, addresses, cancellationToken);
        return Ok(new ApiResponse<UserAddressView>(
            true,
            new UserAddressView(a.Id, a.ReceiverName, a.Phone, a.Region, a.Detail, a.IsDefault),
            null));
    }

    [HttpDelete("{addressId:guid}")]
    public async Task<IActionResult> Delete(Guid addressId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.Unauthorized, "Missing user identity.")));
        }

        var addresses = (await _addressBookRepository.ListAsync(userId, cancellationToken)).ToList();
        var removed = addresses.RemoveAll(a => a.Id == addressId);
        if (removed == 0)
        {
            return NotFound(new ApiResponse<object>(false, null, new ApiError(ApiErrorCodes.NotFound, "Address not found.")));
        }

        if (addresses.Count > 0 && addresses.All(x => !x.IsDefault))
        {
            addresses[0].IsDefault = true;
        }

        await _addressBookRepository.SaveAllAsync(userId, addresses, cancellationToken);
        return Ok(new ApiResponse<object>(true, new { deleted = addressId }, null));
    }

    private bool TryGetUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }
}
