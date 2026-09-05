namespace Turbo_Auth.Models.Accounts;

public sealed class AccountResponse
{
    public int AccountId { get; init; }
    public string? Username { get; init; }
    public string? Email { get; init; }
    public ICollection<Role> UserRoles { get; init; } = [];
}
