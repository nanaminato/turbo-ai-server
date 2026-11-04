using Turbo.Auth.Contracts.Common;
using System.ComponentModel.DataAnnotations;
namespace Turbo.Auth.Contracts.Auth;

public sealed class AccountWriteRequest : IValidatableObject
{
    [Range(0, int.MaxValue)] public int AccountId { get; init; }
    [Required, StringLength(20)] public string Username { get; init; } = "";
    [StringLength(128), DataType(DataType.Password)] public string? Password { get; init; }
    [Required, EmailAddress, StringLength(50)] public string Email { get; init; } = "";
    [NonNullItems] public IReadOnlyList<RoleReferenceRequest>? UserRoles { get; init; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (AccountId == 0 && string.IsNullOrWhiteSpace(Password)) yield return new("A password is required when creating an account.", [nameof(Password)]);
        if (!string.IsNullOrEmpty(Password) && Password.Length < 6) yield return new("Password requires at least 6 characters.", [nameof(Password)]);
    }
}
