using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Turbo.Auth.Controllers.Auth.Body;
using Turbo.Auth.Data.Contexts;
using Turbo.Auth.Repositories.Accounts;
using Turbo.Auth.Security;

namespace Turbo.Auth.Controllers.Auth;

[ApiController]
[Route("api/[controller]")]
public class AccountController: Controller
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
    public async Task<IActionResult> GetAccountsWithRole([FromQuery] int? roleId)
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
    public async Task<IActionResult> GetAccountById(int id)
    {
        try
        {
            var account = await _accountRepository.GetAccountByIdAsync(id);
            return Ok(account);
        }
        catch (Exception)
        {
            return BadRequest("没有该账号");
        }
    }

    [HttpPost]
    [Authorize(Policy = "admin")]
    public async Task<IActionResult> AddAccount(AccountBody account)
    {
        try
        {
            await _accountRepository.AddAccountAsync(account);
            return Ok();
        }
        catch (Exception)
        {
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "账号创建失败" });
        }
    }

    [HttpPut("{accountId}")]
    [Authorize(Policy = "admin")]
    public async Task<IActionResult> UpdateAccount(AccountBody account, int accountId)
    {
        if (accountId != account.AccountId)
            return BadRequest("错误的参数，不匹配的实体。");
        try
        {
            await _accountRepository.UpdateAccountAsync(account);
            return Ok();
        }
        catch (Exception)
        {
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "账号更新失败" });
        }
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordBody body,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)) return Unauthorized();

        var account = await _context.Accounts!.SingleOrDefaultAsync(candidate => candidate.AccountId == accountId,
            cancellationToken);
        if (account is null) return Unauthorized();

        if (_passwordService.Verify(account, body.CurrentPassword!) == PasswordVerificationState.Invalid)
        {
            return BadRequest("当前密码不正确。");
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
        catch (Exception)
        {
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "账号删除失败" });
        }
    }
}
