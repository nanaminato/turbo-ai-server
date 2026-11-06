using Microsoft.EntityFrameworkCore;
using RelaxKon.Auth.Data.Contexts;
namespace RelaxKon.Auth.Security;
public sealed class RoleRuleException(string code) : Exception(code) { public string Code { get; } = code; }
public static class RoleSafety
{
    // A shared database row serializes membership changes across application instances.
    public static async Task Lock(AuthContext db)
    {
        if (db.Database.IsMySql())
            await db.Database.ExecuteSqlRawAsync("SELECT RoleId FROM Roles WHERE Code='admin' FOR UPDATE");
    }
    public static async Task<int[]> ValidateRoles(AuthContext db, IEnumerable<int> roles)
    {
        var ids = roles.ToArray();
        if (ids.Distinct().Count() != ids.Length || ids.Any(id => id <= 0) ||
            await db.Roles!.CountAsync(role => ids.Contains(role.RoleId)) != ids.Length)
            throw new RoleRuleException("invalid_role_assignment");
        return ids;
    }
    public static async Task ProtectLastAdmin(AuthContext db, int accountId, int[] remainingRoles)
    {
        var admin = await db.Roles!.Where(role => (role.Code ?? role.Name) == "admin").Select(role => role.RoleId).SingleOrDefaultAsync();
        if (remainingRoles.Contains(admin)) return;
        if (await db.AccountRoles!.AnyAsync(link => link.AccountId == accountId && link.RoleId == admin) &&
            !await db.AccountRoles.AnyAsync(link => link.AccountId != accountId && link.RoleId == admin))
            throw new RoleRuleException("last_admin_required");
    }
}
