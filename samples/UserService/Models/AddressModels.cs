namespace UserService.Models;

public sealed class UserAddress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ReceiverName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public sealed class CreateUserAddressRequest
{
    public string ReceiverName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public sealed class UpdateUserAddressRequest
{
    public string? ReceiverName { get; set; }
    public string? Phone { get; set; }
    public string? Region { get; set; }
    public string? Detail { get; set; }
    public bool? IsDefault { get; set; }
}

public sealed record UserAddressView(Guid Id, string ReceiverName, string Phone, string Region, string Detail, bool IsDefault);
