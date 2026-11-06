using RelaxKon.Auth.Contracts.Auth;
using RelaxKon.Auth.Models.Accounts;

namespace RelaxKon.Auth.Repositories.Accounts;

public interface IAccountRepository
{
    Task AddUserAccountAsync(Account account);
    Task AddAccountAsync(AccountWriteRequest accountBody);
    Task UpdateAccountAsync(AccountWriteRequest accountBody);
    Task<List<AccountResponse>> GetAccountsAsync();
    Task<AccountResponse> GetAccountByIdAsync(int id);
    Task DeleteAccountByIdAsync(int id);
    Task<List<AccountResponse>> GetAccountsWithRoleAsync(int roleId);
}
