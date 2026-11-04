using Turbo.Auth.Contracts.Common;
using Turbo.Auth.Localization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Turbo.Auth.Contracts.Auth;
using Turbo.Auth.Data.Contexts;
using Turbo.Auth.Models.Accounts;
using Turbo.Auth.Models.Config;
using Turbo.Auth.Repositories.Accounts;
using Turbo.Auth.Security;

namespace Turbo.Auth.Controllers.Auth;

[ApiController]
[Route("api/[controller]")]
public class AuthController : Controller
{
    private readonly AuthContext _context;
    private readonly JwtSettings _jwtSettings;
    private readonly IAccountRepository _accountRepository;
    private readonly IAccountPasswordService _passwordService;
    private readonly IAuthSessionService _sessions;

    public AuthController(AuthContext context, IOptions<JwtSettings> jwtSettings,
        IAccountRepository accountRepository, IAccountPasswordService passwordService, IAuthSessionService sessions)
    {
        _context = context;
        _jwtSettings = jwtSettings.Value;
        _accountRepository = accountRepository;
        _passwordService = passwordService;
        _sessions = sessions;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrWhiteSpace(body.Password)) return Unauthorized();

        var account = await ValidateUserAsync(body.Username, body.Password, cancellationToken);
        if (account is null) return Unauthorized();

        var issue = await _sessions.CreateSessionAsync(account, body.DeviceName, GetIpAddress(), cancellationToken);
        return Ok(await BuildTokenResponseAsync(account, issue, cancellationToken));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Refresh([FromBody] RefreshTokenRequest body, CancellationToken cancellationToken)
    {
        var rotation = await _sessions.RotateAsync(body.RefreshToken ?? string.Empty, body.DeviceName, GetIpAddress(),
            cancellationToken);
        if (!rotation.Succeeded) return Unauthorized();

        return Ok(await BuildTokenResponseAsync(rotation.Account!, rotation.Issue!, cancellationToken));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest body, CancellationToken cancellationToken)
    {
        await _sessions.RevokeByRefreshTokenAsync(body.RefreshToken ?? string.Empty, cancellationToken);
        return NoContent();
    }

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        await _sessions.InvalidateAllAsync(accountId.Value, cancellationToken);
        return NoContent();
    }

    [HttpGet("sessions")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<SessionResponse>>> Sessions(CancellationToken cancellationToken)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        return Ok((await _sessions.GetActiveSessionsAsync(accountId.Value, cancellationToken)).Select(session =>
            new SessionResponse(session.SessionId, session.DeviceName, session.IpAddress, session.CreatedAt, session.LastUsedAt, session.ExpiresAt)).ToList());
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    [Authorize]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        await _sessions.RevokeSessionAsync(accountId.Value, sessionId, cancellationToken);
        return NoContent();
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest body, CancellationToken cancellationToken)
    {
        var exists = await _context.Accounts!.AnyAsync(
            u => u.Email == body.Email && u.Username == body.Username, cancellationToken);
        if (exists) return Conflict(new ApiMessageResponse("account_exists", ApiMessages.Get("account_exists")));

        var account = new Account { Email = body.Email, Username = body.Username, Password = body.Password };
        await _accountRepository.AddUserAccountAsync(account);
        return Ok();
    }

    private async Task<Account?> ValidateUserAsync(string username, string password, CancellationToken cancellationToken)
    {
        var account = await _context.Accounts!.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
        if (account is null) return null;

        var verification = _passwordService.Verify(account, password);
        if (verification == PasswordVerificationState.Invalid) return null;

        if (verification == PasswordVerificationState.ValidNeedsUpgrade)
        {
            account.Password = _passwordService.Hash(account, password);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return account;
    }

    private async Task<TokenResponse> BuildTokenResponseAsync(Account account, RefreshTokenIssue issue, CancellationToken cancellationToken)
    {
        var accessToken = await GenerateAccessTokenAsync(account, issue.SessionId, cancellationToken);
        return new TokenResponse(accessToken, accessToken, issue.RefreshToken, account.AccountId,
            issue.SessionId, _jwtSettings.AccessTokenMinutes * 60, issue.RefreshTokenExpiresAt);
    }

    private async Task<string> GenerateAccessTokenAsync(Account account, Guid sessionId, CancellationToken cancellationToken)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var roles = await _context.AccountRoles!.AsNoTracking()
            .Where(a => a.AccountId == account.AccountId)
            .Select(a => a.Role!.Name!)
            .ToListAsync(cancellationToken);
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, account.Username!),
            new(ClaimTypes.NameIdentifier, account.AccountId.ToString()),
            new(ClaimTypes.Sid, sessionId.ToString()),
            new("sv", account.SecurityStamp),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        roles.ForEach(role => claims.Add(new Claim(ClaimTypes.Role, role)));

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_jwtSettings.AccessTokenMinutes),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private int? GetCurrentAccountId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)
        ? accountId
        : null;

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
