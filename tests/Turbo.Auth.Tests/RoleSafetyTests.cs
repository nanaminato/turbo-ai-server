using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Turbo.Auth.Data.Contexts;
using Turbo.Auth.Models.Accounts;
using Turbo.Auth.Models.Config;
using Turbo.Auth.Contracts.Auth;
using Turbo.Auth.Repositories.Accounts;
using Turbo.Auth.Security;
namespace Turbo.Auth.Tests;
public class RoleSafetyTests
{
    private SqliteConnection connection = null!;
    private AuthContext db = null!;
    private AccountRepository accounts = null!;
    private RoleRepository roles = null!;
    internal static AuthSessionService Sessions(AuthContext db) => new(db,
        new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())), Microsoft.Extensions.Options.Options.Create(new JwtSettings()));
    [SetUp] public async Task Setup()
    {
        connection = new("Data Source=:memory:"); await connection.OpenAsync();
        connection.CreateCollation("utf8mb4_0900_ai_ci", StringComparer.OrdinalIgnoreCase.Compare);
        db = new(new DbContextOptionsBuilder<AuthContext>().UseSqlite(connection).Options, new ConfigurationBuilder().Build());
        await db.Database.EnsureCreatedAsync();
        db.Roles!.AddRange(new Role { RoleId=1, Code="admin", Name="Administrator" }, new Role { RoleId=2,Code="user",Name="User" });
        db.Accounts!.AddRange(new Account { AccountId=1,Username="admin",Password="hash",Email="a@example.com" },new Account { AccountId=2,Username="other",Password="hash",Email="b@example.com" });
        db.AccountRoles!.Add(new AccountRole { AccountId=1,RoleId=1 }); await db.SaveChangesAsync();
        accounts = new(db,new AccountPasswordService(),Sessions(db)); roles = new(db,Sessions(db));
    }
    [TearDown] public async Task Cleanup() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    [Test] public async Task Last_admin_update_is_rejected_without_partial_account_changes()
    {
        var error = Assert.ThrowsAsync<RoleRuleException>(() => accounts.UpdateAccountAsync(new AccountWriteRequest { AccountId=1,Username="changed",Email="changed@example.com",UserRoles=[new RoleReferenceRequest {RoleId=2}] }));
        Assert.That(error!.Code,Is.EqualTo("last_admin_required")); db.ChangeTracker.Clear();
        Assert.That((await db.Accounts!.FindAsync(1))!.Username,Is.EqualTo("admin"));
        Assert.That(await db.AccountRoles!.CountAsync(link=>link.RoleId==1),Is.EqualTo(1));
    }
    [Test] public async Task Last_admin_delete_rejected_but_second_admin_can_be_deleted()
    {
        Assert.That(Assert.ThrowsAsync<RoleRuleException>(()=>accounts.DeleteAccountByIdAsync(1))!.Code,Is.EqualTo("last_admin_required"));
        db.AccountRoles!.Add(new AccountRole { AccountId=2,RoleId=1 }); await db.SaveChangesAsync();
        await accounts.DeleteAccountByIdAsync(1); Assert.That(await db.Accounts!.CountAsync(),Is.EqualTo(1));
    }
    [Test] public async Task Display_name_can_change_without_changing_identity_and_system_roles_cannot_delete()
    {
        await roles.UpdateRoleAsync(new Role { RoleId=1,Code="admin",Name="管理者",Description="System administrator" });
        Assert.That((await db.Roles!.FindAsync(1))!.Code,Is.EqualTo("admin"));
        Assert.That(Assert.ThrowsAsync<RoleRuleException>(()=>roles.DeleteRoleByIdAsync(1))!.Code,Is.EqualTo("system_role_protected"));
        Assert.That(Assert.ThrowsAsync<RoleRuleException>(()=>roles.UpdateRoleAsync(new Role{RoleId=1,Code="owner",Name="Owner"}))!.Code,Is.EqualTo("role_code_immutable"));
        Assert.That((await roles.GetRolesAsync()).Single(role=>role.RoleId==1).MemberCount,Is.EqualTo(1));
    }
    [Test] public async Task Invalid_assignments_and_duplicate_codes_are_rejected()
    {
        Assert.That(Assert.ThrowsAsync<RoleRuleException>(()=>accounts.UpdateAccountAsync(new AccountWriteRequest { AccountId=2,Username="other",Email="b@example.com",UserRoles=[new RoleReferenceRequest{RoleId=2},new RoleReferenceRequest{RoleId=2}] }))!.Code,Is.EqualTo("invalid_role_assignment"));
        await roles.AddRoleAsync("Reviewer","reviewer");
        Assert.That(Assert.ThrowsAsync<RoleRuleException>(()=>roles.AddRoleAsync("Different","reviewer"))!.Code,Is.EqualTo("role_already_exists"));
    }
    [Test, Explicit("Requires isolated MySQL database")]
    public async Task Concurrent_admin_demotions_preserve_one_admin()
    {
        await using var fixture = await MySqlTestDatabase.Create();
        await fixture.Execute("INSERT INTO Roles(RoleId,Code,Name) VALUES(1,'admin','Admin'),(2,'user','User'); INSERT INTO AccountRoles(AccountId,RoleId) VALUES(1,1),(2,1)");
        async Task<bool> Demote(int id)
        {
            await using var context = fixture.Auth();
            try { await new AccountRepository(context,new AccountPasswordService(),Sessions(context)).UpdateAccountAsync(new AccountWriteRequest { AccountId=id,Username=$"test-{id}",Email=$"{id}@example.com",UserRoles=[new RoleReferenceRequest{RoleId=2}] }); return true; }
            catch(RoleRuleException error) when(error.Code=="last_admin_required") { return false; }
        }
        var results = await Task.WhenAll(Demote(1),Demote(2));
        Assert.That(results.Count(value=>value),Is.EqualTo(1));
        Assert.That(await fixture.Scalar<long>("SELECT COUNT(DISTINCT AccountId) FROM AccountRoles WHERE RoleId=1"),Is.EqualTo(1));
    }
    [Test, Explicit("Requires isolated MySQL database")]
    public async Task Legacy_migration_preserves_exact_codes()
    {
        await using var fixture = await MySqlTestDatabase.Create(legacy:true,upgrade:false);
        await fixture.Execute("INSERT INTO Roles(RoleId,Name) VALUES(1,'自定义角色'),(2,'admin')");
        await fixture.Execute(MySqlTestDatabase.Resource("RoleSystem.sql"));
        Assert.That(await fixture.Scalar<string>("SELECT Code FROM Roles WHERE RoleId=1"),Is.EqualTo("自定义角色"));
        Assert.That(await fixture.Scalar<long>("SELECT COUNT(*) FROM Accounts"),Is.EqualTo(2));
    }
    [Test, Explicit("Requires isolated MySQL database")]
    public async Task Migration_rejects_duplicate_legacy_roles_before_persistent_ddl()
    {
        await using var fixture = await MySqlTestDatabase.Create(legacy:true,upgrade:false);
        await fixture.Execute("INSERT INTO Roles(RoleId,Name) VALUES(1,'duplicate'),(2,'duplicate')");
        Assert.ThrowsAsync<MySqlConnector.MySqlException>(() => fixture.Execute(MySqlTestDatabase.Resource("RoleSystem.sql")));
        Assert.That(await fixture.Scalar<long>("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='Roles' AND COLUMN_NAME='Code'"),Is.EqualTo(0));
        Assert.That(await fixture.Scalar<long>("SELECT COUNT(*) FROM Roles"),Is.EqualTo(2));
    }

}

