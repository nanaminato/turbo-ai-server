using Turbo.Auth.Data.Contexts;

namespace Turbo.Auth.Repositories.Accounts;

public class AccountRoleRepository: IAccountRoleRepository
{
    private AuthContext _authContext;

    public AccountRoleRepository(AuthContext authContext)
    {
        _authContext = authContext;
    }
    public Task DeleteAccountRoleLinkWithRoleByIdAsync(int id)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAccountRoleLinkWithRoleByNameAsync(string name)
    {
        throw new NotImplementedException();
    }
}