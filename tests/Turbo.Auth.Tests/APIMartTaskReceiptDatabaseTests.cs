using Microsoft.Extensions.Configuration;
using Turbo.Auth.Bootstrap;
using Turbo.Auth.Security.FeatureAccess;

namespace Turbo.Auth.Tests;

public class APIMartTaskReceiptDatabaseTests
{
    private static FeatureAccessStore Store(MySqlTestDatabase fixture)
    {
        using var connection = fixture.Connection();
        return new FeatureAccessStore(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:ciko"] = connection.ConnectionString }).Build());
    }

    [Test, Explicit("Requires isolated MySQL database")]
    public async Task Receipts_pin_owned_task_routes_and_reject_owner_or_key_overwrites()
    {
        await using var fixture = await MySqlTestDatabase.Create();
        var store = Store(fixture);
        await store.RecordTask(1, "pinned", default, 41);
        await store.RecordTask(1, "pinned", default, 41);
        Assert.That(await store.OwnsTask(1, "pinned", default), Is.True);
        Assert.That(await store.OwnsTask(2, "pinned", default), Is.False);
        Assert.That(await store.GetTaskSupplierKeyId(1, "pinned", default), Is.EqualTo(41));
        Assert.That(await store.GetTaskSupplierKeyId(2, "pinned", default), Is.Null);
        Assert.ThrowsAsync<InvalidOperationException>(() => store.RecordTask(2, "pinned", default, 41));
        Assert.ThrowsAsync<InvalidOperationException>(() => store.RecordTask(1, "pinned", default, 42));
        Assert.ThrowsAsync<InvalidOperationException>(() => store.RecordTask(1, "pinned", default));
        Assert.That(await store.GetTaskSupplierKeyId(1, "pinned", default), Is.EqualTo(41));
        Assert.That(await fixture.Scalar<long>("SELECT COUNT(*) FROM FeatureAccessTasks WHERE TaskId='pinned'"), Is.EqualTo(1));
        await store.RecordTask(1, "legacy", default);
        await store.RecordTask(1, "legacy", default);
        Assert.That(await store.OwnsTask(1, "legacy", default), Is.True);
        Assert.That(await store.GetTaskSupplierKeyId(1, "legacy", default), Is.Null);
        Assert.ThrowsAsync<InvalidOperationException>(() => store.RecordTask(1, "legacy", default, 41));
    }

    [Test, Explicit("Requires isolated MySQL database")]
    public async Task Concurrent_duplicate_receipts_cannot_transfer_ownership()
    {
        await using var fixture = await MySqlTestDatabase.Create();
        var store = Store(fixture);
        async Task<bool> Record(int owner)
        {
            try { await store.RecordTask(owner, "shared-id", default, owner + 40); return true; }
            catch (InvalidOperationException) { return false; }
        }
        var results = await Task.WhenAll(Record(1), Record(2));
        Assert.That(results.Count(value => value), Is.EqualTo(1));
        var owner = results[0] ? 1 : 2;
        Assert.That(await store.GetTaskSupplierKeyId(owner, "shared-id", default), Is.EqualTo(owner + 40));
        Assert.That(await fixture.Scalar<long>("SELECT COUNT(*) FROM FeatureAccessTasks"), Is.EqualTo(1));
    }

    [Test, Explicit("Requires isolated MySQL database")]
    public async Task Additive_upgrade_preserves_legacy_receipts_and_preflight_names_the_missing_upgrade()
    {
        await using var fixture = await MySqlTestDatabase.Create();
        await fixture.Execute("INSERT INTO FeatureAccessTasks(TaskId,AccountId) VALUES('legacy-before-upgrade',1); ALTER TABLE FeatureAccessTasks DROP COLUMN SupplierKeyId");
        using var connection = fixture.Connection();
        var error = Assert.ThrowsAsync<InvalidOperationException>(() => ArchitectureSchemaCheck.Validate(connection.ConnectionString));
        Assert.That(error!.Message, Does.Contain("apimart-task-route-upgrade.sql"));
        await fixture.Execute(MySqlTestDatabase.Resource("ApimartTaskRoute.sql"));
        await ArchitectureSchemaCheck.Validate(connection.ConnectionString);
        Assert.That(await fixture.Scalar<long>("SELECT COUNT(*) FROM FeatureAccessTasks WHERE TaskId='legacy-before-upgrade' AND AccountId=1 AND SupplierKeyId IS NULL"), Is.EqualTo(1));
        Assert.That(await fixture.Scalar<long>("SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='FeatureAccessTasks' AND COLUMN_NAME='SupplierKeyId' AND REFERENCED_TABLE_NAME IS NOT NULL"), Is.Zero);
    }
}
