using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RelaxKon.Auth.Contracts.Auth;
using RelaxKon.Auth.Contracts.Common;
using RelaxKon.Auth.Localization;
using RelaxKon.Auth.Security.Registration;
namespace RelaxKon.Auth.Controllers.Auth;

[ApiController, AllowAnonymous, Route("api/auth")]
public sealed class RegistrationController(IRegistrationService registration) : ControllerBase
{
    [HttpPost("register/email-code"), EnableRateLimiting("registration-email")]
    public async Task<ActionResult<EmailCodeResponse>> SendCode(EmailCodeRequest request, CancellationToken token)
    {
        var result = await registration.SendCode(request.Email, token);
        if (result.Status == EmailCodeStatus.Unavailable) return StatusCode(503, new ApiMessageResponse("registration_unavailable", ApiMessages.Get("registration_unavailable")));
        if (result.Status == EmailCodeStatus.Cooldown)
        {
            Response.Headers.RetryAfter = result.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return StatusCode(429, new EmailCodeResponse("registration_cooldown", ApiMessages.Get("registration_cooldown"), 600, result.RetryAfterSeconds));
        }
        return Accepted(new EmailCodeResponse("registration_code_sent", ApiMessages.Get("registration_code_sent"), 600, 60));
    }
    [HttpPost("register"), EnableRateLimiting("registration")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken token)
    {
        var result = await registration.Register(request, token);
        if (result == RegistrationStatus.Success) return Ok();
        var (status, code) = result switch
        {
            RegistrationStatus.AccountExists => (409, "registration_account_exists"),
            RegistrationStatus.Unavailable => (503, "registration_unavailable"),
            _ => (400, "registration_invalid_code")
        };
        return StatusCode(status, new ApiMessageResponse(code, ApiMessages.Get(code)));
    }
}
