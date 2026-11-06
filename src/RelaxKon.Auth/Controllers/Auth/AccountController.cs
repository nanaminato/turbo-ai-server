using RelaxKon.Auth.Contracts.Common;
using RelaxKon.Auth.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using RelaxKon.Auth.Contracts.Auth;
using RelaxKon.Auth.Data.Contexts;
using RelaxKon.Auth.Repositories.Accounts;
using RelaxKon.Auth.Security;

namespace RelaxKon.Auth.Controllers.Auth;

[ApiController]
[Route("api/[controller]")]
public class AccountController : Controller
{
    private readonly IAccountRepository _accountRepository;
    private readonly AuthContext _context;
    private readonly IAccountPasswordService _passwordService;
    private readonly IAuthSessionService _sessions;

    public AccountController(IAccountRepository accountRepository, AuthContext context,
        IAccountPasswordService passwordService, IAuthSessionService sessions)
    {
        _accountRepository = accountRepository;
        _context = context;
        _passwordService = passwordService;
        _sessions = sessions;
    }
    [HttpGet]
    [Authorize(Policy = "admin")]
    public async Task<ActionResult<IReadOnlyList<AccountResponse>>> GetAccountsWithRole([FromQuery] int? roleId)
    {
        if (roleId == null)
        {
            var accounts = await _accountRepository.GetAccountsAsync();
            return Ok(accounts);
        }

        var accountsWithRole = await _accountRepository.GetAccountsWithRoleAsync(roleId.Value);
        return Ok(accountsWithRole);
    }
    [HttpGet("{id}")]
    [Authorize(Policy = "admin")]
    public async Task<ActionResult<AccountResponse>> GetAccountById(int id)
    {
        try
        {
            var account = await _accountRepository.GetAccountByIdAsync(id);
            return Ok(account);
        }
        catch (RoleRuleException error) { return BadRequest(new CodeResponse(error.Code)); }
        catch (Exception)
        {
            return BadRequest(ApiMessages.Get("account_missing"));
        }
    }

    [HttpPost]
    [Authorize(Policy = "admin")]
    public async Task<IActionResult> AddAccount(AccountWriteRequest account)
    {
        if (account.AccountId != 0) return BadRequest(ApiProblems.Create(400, "invalid_entity"));
        try
        {
            await _accountRepository.AddAccountAsync(account);
            return Ok();
        }
        catch (RoleRuleException error) { return BadRequest(new CodeResponse(error.Code)); }
        catch (Exception)
        {
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = ApiMessages.Get("account_create_failed") });
        }
    }

    [HttpPut("{accountId}")]
    [Authorize(Policy = "admin")]
    public async Task<IActionResult> UpdateAccount(AccountWriteRequest account, int accountId)
    {
        if (accountId != account.AccountId)
            return BadRequest(ApiMessages.Get("invalid_entity"));
        try
        {
            await _accountRepository.UpdateAccountAsync(account);
            return Ok();
        }
        catch (RoleRuleException error) { return BadRequest(new CodeResponse(error.Code)); }
        catch (Exception)
        {
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = ApiMessages.Get("account_update_failed") });
        }
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest body,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)) return Unauthorized();

        var account = await _context.Accounts!.SingleOrDefaultAsync(candidate => candidate.AccountId == accountId,
            cancellationToken);
        if (account is null) return Unauthorized();

        if (_passwordService.Verify(account, body.CurrentPassword!) == PasswordVerificationState.Invalid)
        {
            return BadRequest(ApiMessages.Get("wrong_password"));
        }

        account.Password = _passwordService.Hash(account, body.NewPassword!);
        await _context.SaveChangesAsync(cancellationToken);
        await _sessions.InvalidateAllAsync(accountId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "admin")]
    public async Task<IActionResult> DeleteAccountById(int id)
    {
        try
        {
            await _accountRepository.DeleteAccountByIdAsync(id);
            return Ok();
        }
        catch (RoleRuleException error) { return BadRequest(new CodeResponse(error.Code)); }
        catch (Exception)
        {
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = ApiMessages.Get("account_delete_failed") });
        }
    }
}
