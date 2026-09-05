using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Turbo.Auth.Controllers.Auth.Body;
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
    public async Task<IActionResult> Login([FromBody] SignBody body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrWhiteSpace(body.Password)) return Unauthorized();

        var account = await ValidateUserAsync(body.Username, body.Password, cancellationToken);
        if (account is null) return Unauthorized();

        var issue = await _sessions.CreateSessionAsync(account, body.DeviceName, GetIpAddress(), cancellationToken);
        return Ok(BuildTokenResponse(account, issue));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenBody body, CancellationToken cancellationToken)
    {
        var rotation = await _sessions.RotateAsync(body.RefreshToken ?? string.Empty, body.DeviceName, GetIpAddress(),
            cancellationToken);
        if (!rotation.Succeeded) return Unauthorized();

        return Ok(BuildTokenResponse(rotation.Account!, rotation.Issue!));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenBody body, CancellationToken cancellationToken)
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
    public async Task<IActionResult> Sessions(CancellationToken cancellationToken)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        return Ok(await _sessions.GetActiveSessionsAsync(accountId.Value, cancellationToken));
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
    public async Task<IActionResult> Register([FromBody] RegisterBody body)
    {
        var count = await _context.Accounts!.Where(u => u.Email == body.Email && u.Username == body.Username)
            .CountAsync();
        if (count >= 1) return Conflict("存在相同的Email+用户名组合");

        var account = new Account { Email = body.Email, Username = body.Username, Password = body.Password };
        try
        {
            await _accountRepository.AddUserAccountAsync(account);
            return Ok();
        }
        catch (Exception)
        {
            return BadRequest("服务端错误");
        }
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

    private object BuildTokenResponse(Account account, RefreshTokenIssue issue)
    {
        var accessToken = GenerateAccessToken(account, issue.SessionId);
        return new
        {
            Token = accessToken,
            AccessToken = accessToken,
            RefreshToken = issue.RefreshToken,
            Id = account.AccountId,
            SessionId = issue.SessionId,
            AccessTokenExpiresIn = _jwtSettings.AccessTokenMinutes * 60,
            RefreshTokenExpiresAt = issue.RefreshTokenExpiresAt
        };
    }

    private string GenerateAccessToken(Account account, Guid sessionId)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var roles = _context.AccountRoles!.Where(a => a.AccountId == account.AccountId)
            .Include(role => role.Role).ToList();
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, account.Username!),
            new(ClaimTypes.NameIdentifier, account.AccountId.ToString()),
            new(ClaimTypes.Sid, sessionId.ToString()),
            new("sv", account.SecurityStamp),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        roles.ForEach(role => claims.Add(new Claim(ClaimTypes.Role, role.Role!.Name!)));

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
