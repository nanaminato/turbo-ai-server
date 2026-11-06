using Microsoft.EntityFrameworkCore;
using RelaxKon.Auth.Data.Contexts;
using RelaxKon.Auth.Contracts.Auth;
using RelaxKon.Auth.Exceptions;
using RelaxKon.Auth.Models.Accounts;
using RelaxKon.Auth.Security;

namespace RelaxKon.Auth.Repositories.Accounts;

public class AccountRepository : IAccountRepository
{
    private AuthContext _authContext;
    private readonly IAccountPasswordService _passwordService;
    private readonly IAuthSessionService _sessions;

    private readonly ILogger<AccountRepository>? _logger;
    private readonly IHttpContextAccessor? _http;
    public AccountRepository(AuthContext authContext, IAccountPasswordService passwordService, IAuthSessionService sessions, ILogger<AccountRepository>? logger = null, IHttpContextAccessor? http = null)
    {
        _authContext = authContext;
        _passwordService = passwordService;
        _sessions = sessions; _logger = logger; _http = http;
    }
    public async Task AddUserAccountAsync(Account account)
    {
        await using var transaction = await _authContext.Database.BeginTransactionAsync();
        try
        {
            account.Password = _passwordService.Hash(account, account.Password!);
            await _authContext.Accounts!.AddAsync(account);// 保存更改
            await _authContext.SaveChangesAsync();
            Console.WriteLine(account);
            var role = await _authContext.Roles!.FirstOrDefaultAsync(r => (r.Code ?? r.Name) == "user");
            if (role == null)
            {
                role = new Role { Code = "user", Name = "user" };
                _authContext.Roles!.Add(role);
                await _authContext.SaveChangesAsync();
            }
            // 然后将账户与角色关联起来
            var accountRole = new AccountRole { AccountId = account.AccountId, RoleId = role.RoleId };
            await _authContext.AccountRoles!.AddAsync(accountRole);
            Console.WriteLine(accountRole);
            await _authContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            Console.WriteLine("ERROR: 回滚");
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task AddAccountAsync(AccountWriteRequest body)
    {
        await using var tx = await _authContext.Database.BeginTransactionAsync();
        await RoleSafety.Lock(_authContext);
        var ids = await RoleSafety.ValidateRoles(_authContext, (body.UserRoles ?? []).Select(role => role.RoleId));
        if (await _authContext.Accounts!.AnyAsync(item => item.Username == body.Username)) throw new RoleRuleException("account_already_exists");
        var account = new Account { Username = body.Username, Email = body.Email };
        account.Password = _passwordService.Hash(account, body.Password!);
        _authContext.Accounts.Add(account); await _authContext.SaveChangesAsync();
        _authContext.AccountRoles!.AddRange(ids.Select(id => new AccountRole { AccountId = account.AccountId, RoleId = id }));
        await _authContext.SaveChangesAsync(); await tx.CommitAsync();
        await Audit("create", account.AccountId, [], ids);
    }
    public async Task UpdateAccountAsync(AccountWriteRequest body)
    {
        await using var tx = await _authContext.Database.BeginTransactionAsync();
        await RoleSafety.Lock(_authContext);
        var ids = await RoleSafety.ValidateRoles(_authContext, (body.UserRoles ?? []).Select(role => role.RoleId));
        var account = await _authContext.Accounts!.SingleOrDefaultAsync(item => item.AccountId == body.AccountId) ?? throw new RoleRuleException("account_not_found");
        await RoleSafety.ProtectLastAdmin(_authContext, account.AccountId, ids);
        if (await _authContext.Accounts.AnyAsync(item => item.AccountId != account.AccountId && item.Username == body.Username)) throw new RoleRuleException("account_already_exists");
        account.Username = body.Username; account.Email = body.Email;
        if (!string.IsNullOrWhiteSpace(body.Password)) account.Password = _passwordService.Hash(account, body.Password);
        var links = await _authContext.AccountRoles!.Where(link => link.AccountId == account.AccountId).ToListAsync();
        _authContext.AccountRoles.RemoveRange(links.Where(link => !ids.Contains(link.RoleId)));
        _authContext.AccountRoles.AddRange(ids.Where(id => !links.Any(link => link.RoleId == id)).Select(id => new AccountRole { AccountId = account.AccountId, RoleId = id }));
        await _authContext.SaveChangesAsync();
        await _sessions.InvalidateAllAsync(account.AccountId);
        await tx.CommitAsync();
        await Audit("update", account.AccountId, links.Select(link => link.RoleId), ids);
    }

    // Project only public fields; password hashes and security stamps stay in the database.
    private IQueryable<AccountResponse> AccountResponses(int? roleId = null)
    {
        var query = _authContext.Accounts!.AsNoTracking();
        if (roleId is { } filter) query = query.Where(account => account.UserRoles!.Any(link => link.RoleId == filter));
        return query.Select(account => new AccountResponse
        {
            AccountId = account.AccountId, Username = account.Username, Email = account.Email,
            UserRoles = account.UserRoles!.Where(link => link.Role != null)
                .Select(link => new RoleResponse(link.Role!.RoleId, link.Role.Name, link.Role.Code ?? link.Role.Name,
                    link.Role.Description, link.Role.Code == "admin" || link.Role.Code == "user" || link.Role.Code == "vip", 0)).ToList()
        });
    }

    public Task<List<AccountResponse>> GetAccountsAsync() => AccountResponses().ToListAsync();

    public Task<List<AccountResponse>> GetAccountsWithRoleAsync(int roleId) => AccountResponses(roleId).ToListAsync();

    public async Task<AccountResponse> GetAccountByIdAsync(int id) =>
        await AccountResponses().FirstOrDefaultAsync(account => account.AccountId == id)
        ?? throw new RoleRuleException("account_not_found");

    public async Task DeleteAccountByIdAsync(int id)
    {
        await using var tx = await _authContext.Database.BeginTransactionAsync();
        await RoleSafety.Lock(_authContext);
        var account = await _authContext.Accounts!.SingleOrDefaultAsync(item => item.AccountId == id) ?? throw new RoleRuleException("account_not_found");
        await RoleSafety.ProtectLastAdmin(_authContext, id, []);
        var before = await _authContext.AccountRoles!.Where(link => link.AccountId == id).Select(link => link.RoleId).ToArrayAsync();
        await _sessions.InvalidateAllAsync(id);
        _authContext.Accounts.Remove(account);
        await _authContext.SaveChangesAsync(); await tx.CommitAsync();
        await Audit("delete", id, before, []);
    }
    private async Task Audit(string action, int id, IEnumerable<int> before, IEnumerable<int> after)
    {
        if (_logger is null) return;
        var codes = await _authContext.Roles!.AsNoTracking().ToDictionaryAsync(role => role.RoleId, role => role.Code ?? role.Name);
        _logger.LogInformation("Account role management {Action}; actor {ActorId}; target {AccountId}; role codes {Before} to {After}", action,
            _http?.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, id,
            string.Join(",", before.Select(role => codes.GetValueOrDefault(role))), string.Join(",", after.Select(role => codes.GetValueOrDefault(role))));
    }
}
