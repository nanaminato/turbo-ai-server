using Turbo_Auth.Models.Accounts;

namespace Turbo_Auth.Repositories.Accounts;

public interface IRoleRepository
{
    Task<List<Role>> GetRolesAsync();
    Task<List<Role>> GetRolesOfAccountAsync(int id);
    Task DeleteRoleByIdAsync(int id);
    Task AddRoleAsync(string name);
    Task UpdateRoleAsync(Role role);
}