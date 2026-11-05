using Turbo.Auth.Models.Accounts;

namespace Turbo.Auth.Repositories.Accounts;

public interface IRoleRepository
{
    Task<List<Role>> GetRolesAsync();
    Task<List<Role>> GetRolesOfAccountAsync(int id);
    Task DeleteRoleByIdAsync(int id);
    Task AddRoleAsync(string name, string? code = null, string? description = null);
    Task UpdateRoleAsync(Role role);
}