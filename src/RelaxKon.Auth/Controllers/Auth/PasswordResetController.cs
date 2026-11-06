using RelaxKon.Auth.Contracts.Auth;
using RelaxKon.Auth.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using RelaxKon.Auth.Localization;
using RelaxKon.Auth.Security.PasswordReset;

namespace RelaxKon.Auth.Controllers.Auth;

[ApiController]
[AllowAnonymous]
[Route("api/auth/password-reset")]
[EnableRateLimiting("password-reset")]
public sealed class PasswordResetController(IPasswordResetService service, IOptions<PasswordResetOptions> options) : ControllerBase
{
    [HttpPost("request")]
    public async Task<IActionResult> RequestReset(PasswordResetRequest body, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return Unavailable();
        await service.RequestAsync(body.Username, body.Email, cancellationToken);
        return Accepted(new ApiMessageResponse("reset_requested", ApiMessages.Get("reset_requested")));
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmReset(PasswordResetConfirmationRequest body, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return Unavailable();
        if (!await service.ResetAsync(body.Token, body.NewPassword, cancellationToken))
            return BadRequest(new ApiMessageResponse("reset_invalid", ApiMessages.Get("reset_invalid")));
        return NoContent();
    }

    private ObjectResult Unavailable() => StatusCode(503, new ApiMessageResponse("reset_unavailable", ApiMessages.Get("reset_unavailable")));
}
