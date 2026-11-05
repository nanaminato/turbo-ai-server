using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Turbo.Auth.Data.Contexts;
using Turbo.Auth.Models.Accounts;
using Turbo.Auth.Security;
namespace Turbo.Auth.Repositories.Accounts;
public class RoleRepository(AuthContext database, IAuthSessionService sessions, ILogger<RoleRepository>? logger = null, IHttpContextAccessor? http = null) : IRoleRepository
{
    private string? Actor => http?.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    public async Task<List<Role>> GetRolesAsync()
    {
        var roles = await database.Roles!.AsNoTracking().OrderBy(role => role.RoleId).ToListAsync();
        var counts = await database.AccountRoles!.GroupBy(link => link.RoleId).Select(group => new { Id = group.Key, Count = group.Select(link => link.AccountId).Distinct().Count() }).ToDictionaryAsync(item => item.Id, item => item.Count);
        foreach (var role in roles) role.MemberCount = counts.GetValueOrDefault(role.RoleId);
        return roles;
    }
    public async Task<List<Role>> GetRolesOfAccountAsync(int id) => await database.AccountRoles!.AsNoTracking().Where(link => link.AccountId == id).Select(link => link.Role!).Distinct().ToListAsync();
    public async Task DeleteRoleByIdAsync(int id)
    {
        await using var tx = await database.Database.BeginTransactionAsync();
        await RoleSafety.Lock(database);
        var role = await database.Roles!.SingleOrDefaultAsync(role => role.RoleId == id) ?? throw new RoleRuleException("role_not_found");
        if (role.IsSystem) throw new RoleRuleException("system_role_protected");
        await Turbo.Auth.Security.FeatureAccess.FeatureAccessStore.RemoveRole(database, role.Code ?? role.Name!);
        var links = await database.AccountRoles!.Where(link => link.RoleId == id).ToListAsync();
        var accounts = links.Select(link => link.AccountId).Distinct().ToArray();
        database.AccountRoles.RemoveRange(links); database.Roles.Remove(role);
        await database.SaveChangesAsync();
        foreach (var account in accounts) await sessions.InvalidateAllAsync(account);
        await tx.CommitAsync();
        logger?.LogInformation("Role deleted: actor {ActorId}; target {RoleId} {Code}; affected accounts {Count}", Actor, id, role.Code, accounts.Length);
    }
    public async Task AddRoleAsync(string name, string? code = null, string? description = null)
    {
        name = name.Trim(); code = (code ?? name).Trim();
        if (name.Length is < 1 or > 64 || !Regex.IsMatch(code, "^[a-z][a-z0-9_-]{1,63}$") || description?.Length > 256)
            throw new RoleRuleException("invalid_role");
        await using var tx = await database.Database.BeginTransactionAsync();
        await RoleSafety.Lock(database);
        if (await database.Roles!.AnyAsync(role => role.Code == code || role.Name == name)) throw new RoleRuleException("role_already_exists");
        var role = new Role { Code = code, Name = name, Description = description?.Trim() };
        database.Roles.Add(role); await database.SaveChangesAsync(); await tx.CommitAsync();
        logger?.LogInformation("Role created: actor {ActorId}; target {RoleId} {Code}", Actor, role.RoleId, code);
    }
    public async Task UpdateRoleAsync(Role role)
    {
        var name = role.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 64 || role.Description?.Length > 256) throw new RoleRuleException("invalid_role");
        await using var tx = await database.Database.BeginTransactionAsync();
        await RoleSafety.Lock(database);
        var existing = await database.Roles!.SingleOrDefaultAsync(item => item.RoleId == role.RoleId) ?? throw new RoleRuleException("role_not_found");
        if (role.Code is not null && role.Code != (existing.Code ?? existing.Name)) throw new RoleRuleException("role_code_immutable");
        if (await database.Roles.AnyAsync(item => item.RoleId != role.RoleId && item.Name == name)) throw new RoleRuleException("role_already_exists");
        var before = existing.Name;
        existing.Name = name; existing.Description = role.Description?.Trim();
        await database.SaveChangesAsync(); await tx.CommitAsync();
        logger?.LogInformation("Role updated: actor {ActorId}; target {RoleId} {Code}; name {Before} to {After}", Actor, role.RoleId, existing.Code, before, name);
    }
}
