using Turbo_Auth.Controllers.Auth.Body;
using Turbo_Auth.Models.Accounts;

namespace Turbo_Auth.Repositories.Accounts;

public interface IAccountRepository
{
    Task AddUserAccountAsync(Account account);
    Task AddAccountAsync(AccountBody accountBody);
    Task UpdateAccountAsync(AccountBody accountBody);
    Task<List<dynamic>> GetAccountsAsync();
    Task<dynamic> GetAccountByIdAsync(int id);
    Task DeleteAccountByIdAsync(int id);
    Task<List<dynamic>> GetAccountsWithRoleAsync(int roleId);
}