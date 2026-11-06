using System.ComponentModel.DataAnnotations;
namespace RelaxKon.Auth.Contracts.Auth;

public sealed class EmailCodeRequest { [Required, EmailAddress, StringLength(50)] public string Email { get; init; } = ""; }
public sealed record EmailCodeResponse(string Code, string Message, int ExpiresInSeconds, int ResendAfterSeconds);
