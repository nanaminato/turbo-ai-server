using Turbo_Auth.Context;

namespace Turbo_Auth.Repositories.Accounts;

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