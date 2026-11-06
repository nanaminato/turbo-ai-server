using Microsoft.Extensions.Configuration;
using Turbo.Auth.Security.FeatureAccess;

namespace Turbo.Auth.Tests;

public class FeatureUsageDatabaseTests
{
    [Test, Explicit("Requires isolated MySQL database; creates and drops only a generated test database.")]
    public async Task Usage_snapshot_is_account_scoped_read_only_and_excludes_expired_leases()
    {
        await using var fixture = await MySqlTestDatabase.Create();
        await fixture.Execute("""
            INSERT INTO Roles(RoleId,Code,Name) VALUES(1,'admin','Admin'),(2,'user','User');
            INSERT INTO AccountRoles(AccountId,RoleId) VALUES(1,2),(2,1);
            """);
        using var scoped = fixture.Connection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:ciko"] = scoped.ConnectionString }).Build();
        var store = new FeatureAccessStore(configuration);
        var document = FeaturePolicies.Empty;
        document.Features[0].Roles["user"] = new(true, 10, 100, 3);
        Assert.That(await store.Save(document, default), Is.True);
        var policy = FeaturePolicies.Resolve(document, "chat", ["user"]);
        Assert.That((await store.Acquire(1, policy, default)).LeaseId, Is.Not.Null);
        Assert.That((await store.Acquire(2, policy, default)).LeaseId, Is.Not.Null);
        await fixture.Execute("""
            INSERT INTO FeatureAccessLeases(LeaseId,AccountId,FeatureKey,ExpiresAt)
            VALUES('expired',1,'chat',DATE_SUB(UTC_TIMESTAMP(),INTERVAL 1 SECOND));
            INSERT INTO FeatureAccessUsage(AccountId,FeatureKey,MinuteWindow,DayWindow,MinuteCount,DayCount)
            VALUES(1,'image',FLOOR(UNIX_TIMESTAMP()/60)-1,FLOOR(UNIX_TIMESTAMP()/86400)-1,9,99);
            """);
        var before = await fixture.Rows("SELECT * FROM FeatureAccessUsage ORDER BY AccountId,FeatureKey");
        var leases = await fixture.Scalar<long>("SELECT COUNT(*) FROM FeatureAccessLeases");
        var reader = new FeatureUsageReader(configuration);
        var first = await reader.ReadUsage(1, default);
        var chat = first.Features.Single(feature => feature.Policy.Key == "chat");
        Assert.Multiple(() =>
        {
            Assert.That(first.PolicyRevision, Is.EqualTo(1));
            Assert.That(chat.DayUsed, Is.EqualTo(1));
            Assert.That(chat.ActiveRequests, Is.EqualTo(1));
            Assert.That(chat.ConcurrentRemaining, Is.EqualTo(2));
            Assert.That(first.Features.Single(feature => feature.Policy.Key == "image").DayUsed, Is.Zero);
        });
        var admin = await reader.ReadUsage(2, default);
        Assert.That(admin.Features[0].Policy.Enabled, Is.False, "Admin is not an AI entitlement bypass.");
        Assert.That((await reader.ReadUsage(99, default)).Features.All(feature => feature.DayUsed == 0), Is.True);
        Assert.That(await fixture.Rows("SELECT * FROM FeatureAccessUsage ORDER BY AccountId,FeatureKey"), Is.EqualTo(before));
        Assert.That(await fixture.Scalar<long>("SELECT COUNT(*) FROM FeatureAccessLeases"), Is.EqualTo(leases));
        Assert.That((await store.Read(default)).Revision, Is.EqualTo(1));
    }

    [Test, Explicit("Requires isolated MySQL database; creates and drops only a generated test database.")]
    public async Task Usage_snapshot_reads_current_role_codes_and_grants_and_handles_empty_policy_without_seeding()
    {
        await using var fixture = await MySqlTestDatabase.Create();
        using var scoped = fixture.Connection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:ciko"] = scoped.ConnectionString }).Build();
        var reader = new FeatureUsageReader(configuration);
        Assert.That((await reader.ReadUsage(1, default)).Features.All(feature => !feature.Policy.Enabled), Is.True);
        Assert.That(await fixture.Scalar<long>("SELECT COUNT(*) FROM FeatureAccessPolicies"), Is.Zero);
        await fixture.Execute("""
            INSERT INTO Roles(RoleId,Code,Name) VALUES(1,'admin','Admin'),(2,'user','User'),(3,'vip','VIP');
            INSERT INTO AccountRoles(AccountId,RoleId) VALUES(1,2);
            """);
        var store = new FeatureAccessStore(configuration);
        var document = FeaturePolicies.Empty;
        document.Features[0].Roles["user"] = new(true, 1, 5, 1);
        document.Features[0].Roles["vip"] = new(true);
        Assert.That(await store.Save(document, default), Is.True);
        Assert.That((await reader.ReadUsage(1, default)).Features[0].DayRemaining, Is.EqualTo(5));
        await fixture.Execute("INSERT INTO AccountRoles(AccountId,RoleId) VALUES(1,3)");
        Assert.That((await reader.ReadUsage(1, default)).Features[0].DayRemaining, Is.Null);
        await fixture.Execute("DELETE FROM AccountRoles WHERE AccountId=1");
        Assert.That((await reader.ReadUsage(1, default)).Features[0].DayRemaining, Is.Zero);
    }
}
