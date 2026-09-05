using Turbo.Auth.Controllers.Auth.Body;
using Turbo.Auth.Models.Accounts;

namespace Turbo.Auth.Repositories.Accounts;

public interface IAccountRepository
{
    Task AddUserAccountAsync(Account account);
    Task AddAccountAsync(AccountBody accountBody);
    Task UpdateAccountAsync(AccountBody accountBody);
    Task<List<AccountResponse>> GetAccountsAsync();
    Task<AccountResponse> GetAccountByIdAsync(int id);
    Task DeleteAccountByIdAsync(int id);
    Task<List<AccountResponse>> GetAccountsWithRoleAsync(int roleId);
}
