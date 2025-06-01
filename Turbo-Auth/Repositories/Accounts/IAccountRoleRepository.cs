namespace Turbo_Auth.Repositories.Accounts;

public interface IAccountRoleRepository
{
    Task DeleteAccountRoleLinkWithRoleByIdAsync(int id);
    Task DeleteAccountRoleLinkWithRoleByNameAsync(string name);
}