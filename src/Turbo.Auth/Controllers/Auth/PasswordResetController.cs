using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Turbo.Auth.Localization;
using Turbo.Auth.Security.PasswordReset;

namespace Turbo.Auth.Controllers.Auth;

[ApiController]
[AllowAnonymous]
[Route("api/auth/password-reset")]
[EnableRateLimiting("password-reset")]
public sealed class PasswordResetController(IPasswordResetService service, IOptions<PasswordResetOptions> options) : ControllerBase
{
    [HttpPost("request")]
    public async Task<IActionResult> RequestReset(ResetRequest body, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return Unavailable();
        await service.RequestAsync(body.Username, body.Email, cancellationToken);
        return Accepted(new { code = "reset_requested", message = ApiMessages.Get("reset_requested") });
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmReset(ResetConfirmation body, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return Unavailable();
        if (!await service.ResetAsync(body.Token, body.NewPassword, cancellationToken))
            return BadRequest(new { code = "reset_invalid", message = ApiMessages.Get("reset_invalid") });
        return NoContent();
    }

    private ObjectResult Unavailable() => StatusCode(503, new { code = "reset_unavailable", message = ApiMessages.Get("reset_unavailable") });
}

public sealed class ResetRequest
{
    [Required, StringLength(20, MinimumLength = 3)] public string Username { get; set; } = "";
    [Required, EmailAddress, StringLength(50)] public string Email { get; set; } = "";
}

public sealed class ResetConfirmation
{
    [Required, StringLength(4096)] public string Token { get; set; } = "";
    [Required, StringLength(20, MinimumLength = 8)] public string NewPassword { get; set; } = "";
    [Required, Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = "";
}
