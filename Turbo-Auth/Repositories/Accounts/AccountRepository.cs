using Microsoft.EntityFrameworkCore;
using Turbo_Auth.Context;
using Turbo_Auth.Controllers.Auth.Body;
using Turbo_Auth.Exceptions;
using Turbo_Auth.Models.Accounts;

namespace Turbo_Auth.Repositories.Accounts;

public class AccountRepository: IAccountRepository
{
    private AuthContext _authContext;
    public AccountRepository(AuthContext authContext)
    {
        _authContext = authContext;
    }
    public async Task AddUserAccountAsync(Account account)
    {
        await using var transaction = await _authContext.Database.BeginTransactionAsync();
        try
        {
            await _authContext.Accounts!.AddAsync(account);// 保存更改
            await _authContext.SaveChangesAsync();
            Console.WriteLine(account);
            var role = await _authContext.Roles!.FirstOrDefaultAsync(r => r.Name == "user");
            if (role == null)
            {
                role = new Role { Name = "user" };
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

    public async Task AddAccountAsync(AccountBody accountBody)
    {
        try
        {
            var account = new Account()
            {
                Username = accountBody.Username,
                Password = accountBody.Password,
                Email = accountBody.Email
            };
            await _authContext.Accounts!.AddAsync(account);
            await _authContext.SaveChangesAsync();
            if (accountBody.UserRoles == null) return;
            foreach (var role in accountBody.UserRoles!)
            {
                var accountRole = new AccountRole()
                {
                    RoleId = role.RoleId,
                    AccountId = account.AccountId,
                };
                await _authContext.AccountRoles!.AddAsync(accountRole);
                await _authContext.SaveChangesAsync();
            }
        }
        catch (Exception e)
        {
            throw new NotMatchException();
        }
    }

    public async Task UpdateAccountAsync(AccountBody accountBody)
    {
        try
        {
            var account = await _authContext.Accounts!.FirstOrDefaultAsync(a => a.AccountId == accountBody.AccountId);
            if (account == null)
            {
                throw new Exception("数据实体不存在");
            }

            account.Username = accountBody.Username;
            account.Password = accountBody.Password;
            account.Email = accountBody.Email;
            await _authContext.SaveChangesAsync();
            accountBody.UserRoles ??= new List<Role>();
            _authContext.AccountRoles!.RemoveRange(
                await _authContext.AccountRoles!
                    .Where(ar => ar.AccountId == account.AccountId)
                    .ToListAsync());
            await _authContext.SaveChangesAsync();
            
            foreach (var role in accountBody.UserRoles!)
            {
                var accountRole = new AccountRole()
                {
                    RoleId = role.RoleId,
                    AccountId = account.AccountId,
                };
                await _authContext.AccountRoles!.AddAsync(accountRole);
                await _authContext.SaveChangesAsync();
            }
        }
        catch (Exception e)
        {
            throw new NotMatchException();
        }
    }

    public async Task<List<dynamic>> GetAccountsAsync()
    {
        
        var accounts =  await _authContext.Accounts!.ToListAsync();
        var list = new List<dynamic>();
        foreach (var account in accounts)
        {
            var roles = await _authContext.AccountRoles!
                .Where(a => a.AccountId == account.AccountId)
                .Select(r => r.Role).ToListAsync();
            list.Add(new
            {
                account.AccountId,
                account.Username,
                account.Email,
                account.Password,
                UserRoles = roles 
            });
        }

        return list;
    }
    public async Task<List<dynamic>> GetAccountsWithRoleAsync(int roleId)
    {
        var accounts = await _authContext.AccountRoles!.Where(r => r.RoleId == roleId)
            .Select(a => a.Account).ToListAsync();
        var list = new List<dynamic>();
        foreach (var account in accounts)
        {
            var roles = await _authContext.AccountRoles!
                .Where(a => a.AccountId == account!.AccountId)
                .Select(r => r.Role).ToListAsync();
            list.Add(new
            {
                account!.AccountId,
                account.Username,
                account.Email,
                account.Password,
                UserRoles = roles 
            });
        }

        return list;
    }

    public async Task<dynamic> GetAccountByIdAsync(int id)
    {
        var account = await _authContext.Accounts!.FirstOrDefaultAsync(a => a.AccountId == id);
        var roles = await _authContext.AccountRoles!
            .Where(a => a.AccountId == id)
            .Select(r => r.Role).ToListAsync();
        if (account == null)
        {
            throw new Exception();
        }
        return new
        {
            account.AccountId,
            account.Username,
            account.Email,
            account.Password,
            UserRoles = roles 
        };
    }

    public async Task DeleteAccountByIdAsync(int id)
    {
        var account = await _authContext.Accounts!.Where(a => a.AccountId == id).Include(a=>a.UserRoles)!.ThenInclude(a=>a.Role).FirstOrDefaultAsync();
        if (account != null)
        {
            foreach (var userRole in account.UserRoles!)
            {
                if (userRole.Role!.Name == "admin")
                {
                    throw new UnauthorizedAccessException("不允许删除管理员账户");
                }
            }
            _authContext.Accounts!.Remove(account);
        }
        await _authContext.SaveChangesAsync();
    }

    
}