namespace Turbo.Auth.Contracts.Auth;

public sealed class AccountResponse
{
    public int AccountId { get; init; }
    public string? Username { get; init; }
    public string? Email { get; init; }
    public IReadOnlyList<RoleResponse> UserRoles { get; init; } = [];
}
