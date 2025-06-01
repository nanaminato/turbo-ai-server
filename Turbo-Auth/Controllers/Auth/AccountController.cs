using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo_Auth.Controllers.Auth.Body;
using Turbo_Auth.Repositories.Accounts;

namespace Turbo_Auth.Controllers.Auth;

[ApiController]
[Route("api/[controller]")]
public class AccountController: Controller
{
    private IAccountRepository _accountRepository;

    public AccountController(IAccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
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
        catch (Exception e)
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
        catch (Exception e)
        {
            return BadRequest(e.Message);
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
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
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
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }
}