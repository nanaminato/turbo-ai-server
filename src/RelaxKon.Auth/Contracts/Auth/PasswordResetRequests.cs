using System.ComponentModel.DataAnnotations;
namespace RelaxKon.Auth.Contracts.Auth;

public sealed class PasswordResetRequest
{
    [Required, StringLength(20, MinimumLength = 3)] public string Username { get; set; } = "";
    [Required, EmailAddress, StringLength(50)] public string Email { get; set; } = "";
}

public sealed class PasswordResetConfirmationRequest
{
    [Required, StringLength(4096)] public string Token { get; set; } = "";
    [Required, StringLength(20, MinimumLength = 8)] public string NewPassword { get; set; } = "";
    [Required, Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = "";
}
