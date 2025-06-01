using Microsoft.EntityFrameworkCore;
using Turbo_Auth.Context;
using Turbo_Auth.Exceptions;
using Turbo_Auth.Models.Accounts;

namespace Turbo_Auth.Repositories.Accounts;

public class RoleRepository: IRoleRepository
{
    private AuthContext _authContext;
    public RoleRepository(AuthContext authContext)
    {
        _authContext = authContext;
    }
    public async Task<List<Role>> GetRolesAsync()
    {
        return await _authContext.Roles!.ToListAsync();
    }

    public async Task<List<Role>> GetRolesOfAccountAsync(int id)
    {
        return await _authContext.AccountRoles!.Where(a => a.AccountId == id)
            .Select(a => a.Role!).ToListAsync();
    }

    public async Task DeleteRoleByIdAsync(int id)
    {
        var role = await _authContext.Roles!.Where(r => r.RoleId == id).FirstOrDefaultAsync();
        if (role == null)
        {
            
        }
        else
        {
            var accountRoles = await _authContext.AccountRoles!
                .Where(a => a.RoleId == id).ToListAsync();
            _authContext.AccountRoles!.RemoveRange(accountRoles);
            await _authContext.SaveChangesAsync();
            _authContext.Roles!.Remove(role);
            await _authContext.SaveChangesAsync();
        }
    }

    public async Task AddRoleAsync(string name)
    {
        var exist = await _authContext.Roles!
            .Where(r => r.Name == name).AnyAsync();
        if (exist)
        {
            throw new AlreadyExistException();
        }

        await _authContext.Roles!.AddAsync(new Role()
        {
            Name = name
        });
        await _authContext.SaveChangesAsync();
    }

    public async Task UpdateRoleAsync(Role role)
    {
        var r = await _authContext.Roles!.Where(r => r.RoleId == role.RoleId).FirstOrDefaultAsync();
        if (r == null)
        {
            throw new EntityNotFoundException();
        }

        r.Name = role.Name;
        await _authContext.SaveChangesAsync();
    }
}