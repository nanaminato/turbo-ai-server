using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Newtonsoft.Json.Linq;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Application.Sync;

namespace Turbo.Auth.Tests;

public class ArchitectureDatabaseTests
{
    private const string RequiresMySql = "Requires TURBO_TEST_MYSQL; creates and removes isolated databases.";

    [Test, Explicit(RequiresMySql)]
    public async Task Startup_preflight_explains_missing_upgrade_without_modifying_the_database()
    {
        await using var database = await MySqlTestDatabase.Create(legacy: true, upgrade: false);
        var before = await database.Scalar<long>("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE()");
        using var connection = database.Connection();
        var error = Assert.ThrowsAsync<InvalidOperationException>(() => Turbo.Auth.Bootstrap.ArchitectureSchemaCheck.Validate(connection.ConnectionString));
        Assert.That(error!.Message, Does.Contain("RoutingConfiguration").And.Contain("architecture-upgrade.sql").And.Not.Contain("Password="));
        Assert.That(await database.Scalar<long>("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE()"), Is.EqualTo(before));
        Assert.That(await database.Scalar<long>("SELECT COUNT(*) FROM ChatMessages"), Is.EqualTo(2));
        await database.Execute(MySqlTestDatabase.Resource("ArchitectureUpgrade.sql"));
        await database.Execute(MySqlTestDatabase.Resource("RoutingWeight.sql"));
        await database.Execute(MySqlTestDatabase.Resource("RoleSystem.sql"));
        await database.Execute(MySqlTestDatabase.Resource("FeatureAccess.sql"));
        await database.Execute(MySqlTestDatabase.Resource("ApimartTaskRoute.sql"));
        await database.Execute(MySqlTestDatabase.Resource("PerformanceIndexes.sql"));
        await Turbo.Auth.Bootstrap.ArchitectureSchemaCheck.Validate(connection.ConnectionString);
    }

    [Test, Explicit(RequiresMySql)]
    public async Task Startup_preflight_accepts_fresh_schema_and_rejects_a_missing_revision_seed()
    {
        await using var database = await MySqlTestDatabase.Create();
        using var connection = database.Connection();
        await Turbo.Auth.Bootstrap.ArchitectureSchemaCheck.Validate(connection.ConnectionString);
        await database.Execute("DELETE FROM RoutingConfiguration WHERE Id=1");
        var error = Assert.ThrowsAsync<InvalidOperationException>(() => Turbo.Auth.Bootstrap.ArchitectureSchemaCheck.Validate(connection.ConnectionString));
        Assert.That(error!.Message, Does.Contain("row Id=1"));
    }

    [TestCase(false), TestCase(true), Explicit(RequiresMySql)]
    public async Task Mysql_schema_triggers_and_parallel_sync_commits_are_consistent(bool upgraded)
    {
        await using var database = await MySqlTestDatabase.Create(legacy: upgraded);
        var mutation = History("shared operation");
        var revisions = await Task.WhenAll(Enumerable.Range(0, 24).Select(async _ =>
        {
            await using var scope = database.Auth();
            return await new DataSyncService(scope).Apply(1, mutation, default);
        }));
        Assert.That(revisions, Is.All.EqualTo(1));
        Assert.That(await database.Scalar<long>("SELECT COUNT(*) FROM SyncChanges"), Is.EqualTo(1));

        var committed = new ConcurrentBag<long>();
        await Parallel.ForEachAsync(Enumerable.Range(1, 205), new ParallelOptions {MaxDegreeOfParallelism = 16}, async (index, token) =>
        {
            await using var scope = database.Auth();
            committed.Add(await new DataSyncService(scope).Apply(1,
                new(Guid.NewGuid(), "message", "upsert", (1000 + index).ToString(), 100,
                    JObject.FromObject(new {role = "user", content = $"message{index}", finish = true})), token));
        });
        Assert.That(committed.Order(), Is.EqualTo(Enumerable.Range(2, 205).Select(value => (long)value)));
        await using (var verification = database.Auth())
        {
            var sync = new DataSyncService(verification);
            var first = await sync.Read(1, 1, default);
            Assert.That(first.Changes, Has.Count.EqualTo(200));
            Assert.That(first.Cursor, Is.EqualTo(201));
            Assert.That(first.HasMore, Is.True);
            var second = await sync.Read(1, first.Cursor, default);
            Assert.That(second.Changes.Select(item => item.Version), Is.EqualTo(Enumerable.Range(202, 5).Select(value => (long)value)));
            Assert.That(second.Cursor, Is.EqualTo(206));
            Assert.That(second.HasMore, Is.False);
            Assert.ThrowsAsync<SyncConflictException>(() => sync.Apply(1, mutation with {Payload = JObject.FromObject(new {title = "different payload"})}, default));
        }
        await using (var verification = database.Auth())
        {
            var sync = new DataSyncService(verification);
            await sync.Apply(1, new(Guid.NewGuid(), "message", "delete", "200", 100), default);
            var deletion = (await sync.Read(1, 206, default)).Changes.Single();
            Assert.That(deletion.Action, Is.EqualTo("delete"));
            Assert.That(deletion.HistoryId, Is.EqualTo(100));
            var other = await sync.Read(2, 0, default);
            Assert.That(other.Changes.Single(item => item.Kind == "message").Payload!["content"]!.ToString(), Is.EqualTo("private message"));
            Assert.That(await verification.ChatMessages!.CountAsync(item => item.ChatHistoryId == 1), Is.EqualTo(205));
        }
        TestContext.Progress.WriteLine("24 concurrent retries deduplicated; 205 commits contiguous; 200/5 pagination and account-scoped deletion verified.");
    }

    [TestCase(false), TestCase(true), Explicit(RequiresMySql)]
    public async Task Mysql_all_route_triggers_and_two_replicas_follow_commits(bool upgraded)
    {
        await using var database = await MySqlTestDatabase.Create(legacy: upgraded);
        Assert.That(await database.Scalar<long>("SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME LIKE 'routing_%'"), Is.EqualTo(12));
        string[] operations = [
            "INSERT INTO Models(ModelId,Name,IsChatModel,ModelValue,Vision) VALUES (1,'route',1,'route-model',0)",
            "INSERT INTO SupplierKeys(SupplierKeyId,BaseUrl,RequestIdentifier,Enable,ApiKey) VALUES (1,'https://example.invalid',0,1,'fixture-only')",
            "INSERT INTO ModelKeyBinds(ModelKeyBindId,Enable,SupplierKeyId,ModelId,Fee,ProviderModelValue,Priority) VALUES (1,1,1,1,1,'provider-model',0)",
            "INSERT INTO AvailableModels(ModelId,Enable,Name,IsChatModel,ModelValue,Vision) VALUES (1,1,'available',1,'available-model',0)",
            "UPDATE Models SET Name='changed' WHERE ModelId=1",
            "UPDATE SupplierKeys SET BaseUrl='https://changed.invalid' WHERE SupplierKeyId=1",
            "UPDATE ModelKeyBinds SET Fee=2 WHERE ModelKeyBindId=1",
            "UPDATE AvailableModels SET Name='changed' WHERE ModelId=1",
            "DELETE FROM ModelKeyBinds WHERE ModelKeyBindId=1",
            "DELETE FROM AvailableModels WHERE ModelId=1",
            "DELETE FROM SupplierKeys WHERE SupplierKeyId=1",
            "DELETE FROM Models WHERE ModelId=1"
        ];
        foreach (var operation in operations)
        {
            var before = await Revision();
            await database.Execute(operation);
            Assert.That(await Revision(), Is.EqualTo(before + 1), operation.Split(' ')[0] + " route trigger");
        }
        foreach (var operation in operations.Take(3)) await database.Execute(operation);
        var replicas = new[] {new QuickModel(), new QuickModel()};
        var gates = new[] {new RouteReloadGate(TimeProvider.System), new RouteReloadGate(TimeProvider.System)};
        async Task Refresh(int index)
        {
            await using var scope = database.Keys();
            await new KeyLoader(scope, new StableKeyPoolRepository(new ModelKeyBuilder(), replicas[index]), gates[index]).LoadKeys();
        }
        async Task RefreshBoth() { await Task.WhenAll(Refresh(0), Refresh(1)); }
        await RefreshBoth();
        Assert.That(replicas.All(item => item.IsModelAvailable("route-model")), Is.True);
        Assert.That(replicas.Select(item => item.GetModelAndKey("route-model").Model), Is.All.EqualTo("provider-model"));
        var revision = await Revision();
        await using (var connection = database.Connection())
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var change = new MySqlCommand("UPDATE SupplierKeys SET Enable=0 WHERE SupplierKeyId=1", connection, transaction);
            await change.ExecuteNonQueryAsync();
            await transaction.RollbackAsync();
        }
        Assert.That(await Revision(), Is.EqualTo(revision));
        await RefreshBoth();
        Assert.That(replicas.All(item => item.IsModelAvailable("route-model")), Is.True);
        await database.Execute("UPDATE SupplierKeys SET Enable=0 WHERE SupplierKeyId=1");
        await RefreshBoth();
        Assert.That(replicas.All(item => !item.IsModelAvailable("route-model")), Is.True);
        await database.Execute("UPDATE SupplierKeys SET Enable=1 WHERE SupplierKeyId=1; UPDATE ModelKeyBinds SET ProviderModelValue='renamed-upstream' WHERE ModelKeyBindId=1");
        await RefreshBoth();
        Assert.That(replicas.Select(item => item.GetModelAndKey("route-model").Model), Is.All.EqualTo("renamed-upstream"));
        await database.Execute("DELETE FROM Models WHERE ModelId=1");
        await RefreshBoth();
        Assert.That(replicas.All(item => !item.IsModelAvailable("route-model")), Is.True);
        Assert.That(await database.Scalar<long>("SELECT COUNT(*) FROM ModelKeyBinds"), Is.Zero);
        TestContext.Progress.WriteLine("All 12 routing triggers, transaction rollback, two-replica key disable/re-enable and cascaded route deletion verified.");
        Task<long> Revision() => database.Scalar<long>("SELECT Version FROM RoutingConfiguration WHERE Id=1");
    }

    [Test, Explicit(RequiresMySql)]
    public async Task Mysql_upgrade_matches_fresh_schema_and_preserves_legacy_records()
    {
        await using var upgraded = await MySqlTestDatabase.Create(legacy: true);
        await using var fresh = await MySqlTestDatabase.Create();
        string[] fingerprints = [
            "SELECT TABLE_NAME,COLUMN_NAME,ORDINAL_POSITION,COLUMN_TYPE,IS_NULLABLE,COLUMN_DEFAULT,EXTRA,CHARACTER_SET_NAME,COLLATION_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() ORDER BY TABLE_NAME,ORDINAL_POSITION",
            "SELECT TABLE_NAME,INDEX_NAME,NON_UNIQUE,SEQ_IN_INDEX,COLUMN_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() ORDER BY TABLE_NAME,INDEX_NAME,SEQ_IN_INDEX",
            "SELECT TABLE_NAME,CONSTRAINT_NAME,COLUMN_NAME,REFERENCED_TABLE_NAME,REFERENCED_COLUMN_NAME FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA=DATABASE() ORDER BY TABLE_NAME,CONSTRAINT_NAME,ORDINAL_POSITION",
            "SELECT TRIGGER_NAME,EVENT_MANIPULATION,EVENT_OBJECT_TABLE,ACTION_TIMING,ACTION_STATEMENT FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() ORDER BY TRIGGER_NAME"
        ];
        foreach (var query in fingerprints) Assert.That(await upgraded.Rows(query), Is.EqualTo(await fresh.Rows(query)));
        await using var scope = upgraded.Auth();
        var snapshot = await new DataSyncService(scope).Read(1, 0, default);
        Assert.That(snapshot.Reset, Is.True);
        Assert.That(snapshot.Cursor, Is.Zero);
        Assert.That(snapshot.Changes.Select(item => item.Kind), Is.EquivalentTo(new[] {"history", "message", "task"}));
        Assert.That(snapshot.Changes.Single(item => item.Kind == "history").Payload!["title"]!.ToString(), Is.EqualTo("legacy history"));
        var message = snapshot.Changes.Single(item => item.Kind == "message");
        Assert.That(message.Payload!["content"]!.ToString(), Is.EqualTo("legacy message"));
        Assert.That(message.Payload["fileList"]![0]!["parsedContent"]!.ToString(), Is.EqualTo("preserved attachment"));
        Assert.That(await upgraded.Scalar<long>("SELECT FileAddsId FROM FileAdds WHERE ChatMessageId=1"), Is.EqualTo(1));
        Assert.That(snapshot.Changes.Single(item => item.Kind == "task").Payload!["images"]![0]!.ToString(), Is.EqualTo("https://example.invalid/image"));
        var repeat = Assert.ThrowsAsync<MySqlException>(() => upgraded.Execute(MySqlTestDatabase.Resource("ArchitectureUpgrade.sql")));
        Assert.That(repeat!.Number, Is.EqualTo(1061)); // Duplicate index: fail without silently reapplying the upgrade.
        Assert.That(await upgraded.Scalar<long>("SELECT COUNT(*) FROM ChatMessages"), Is.EqualTo(2));
        TestContext.Progress.WriteLine("Pre-upgrade e01436d schema migrated; columns/indexes/FKs/triggers match fresh initialization; legacy content, attachments and task preserved; repeat rejected.");
    }

    [TestCase("history"), TestCase("message"), Explicit(RequiresMySql)]
    public async Task Mysql_upgrade_rejects_duplicate_client_ids_without_deleting_records(string kind)
    {
        await using var database = await MySqlTestDatabase.Create(legacy: true, upgrade: false);
        await database.Execute(kind == "history"
            ? "INSERT INTO ChatHistories(ChatHistoryId,UserId,DataId,Title) VALUES (3,1,100,'duplicate')"
            : "INSERT INTO ChatMessages(ChatMessageId,ChatHistoryId,DataId,Role,Content,ShowType,Finish) VALUES (3,1,200,'user','duplicate',0,1)");
        var failure = Assert.ThrowsAsync<MySqlException>(() => database.Execute(MySqlTestDatabase.Resource("ArchitectureUpgrade.sql")));
        Assert.That(failure!.Number, Is.EqualTo(1062));
        Assert.That(await database.Scalar<long>(kind == "history" ? "SELECT COUNT(*) FROM ChatHistories" : "SELECT COUNT(*) FROM ChatMessages"), Is.EqualTo(3));
        Assert.That(await database.Scalar<string>("SELECT ParsedContent FROM FileAdds WHERE FileAddsId=1"), Is.EqualTo("preserved attachment"));
        Assert.That(await database.Scalar<long>("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('SyncAccounts','SyncChanges','RoutingConfiguration')"), Is.Zero);
        Assert.That(await database.Scalar<long>("SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND INDEX_NAME='UX_ChatHistories_UserId_DataId'"), Is.EqualTo(kind == "history" ? 0 : 2));
        TestContext.Progress.WriteLine($"Duplicate {kind} IDs rejected; original rows preserved; partial DDL boundary confirmed.");
    }

    [Test, Explicit(RequiresMySql)]
    public async Task Mysql_failed_attachment_write_rolls_back_message_cursor_and_change_log()
    {
        await using var database = await MySqlTestDatabase.Create(legacy: true);
        await database.Execute("CREATE TRIGGER reject_test_attachment BEFORE INSERT ON FileAdds FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='fixture attachment rejection'");
        await using (var scope = database.Auth())
            Assert.ThrowsAsync<DbUpdateException>(() => new DataSyncService(scope).Apply(1,
                new(Guid.NewGuid(), "message", "upsert", "200", 100, JObject.FromObject(new {role = "user", content = "replacement", fileList = new[] {new {fileName = "reject.txt"}}})), default));
        Assert.That(await database.Scalar<string>("SELECT Content FROM ChatMessages WHERE ChatMessageId=1"), Is.EqualTo("legacy message"));
        Assert.That(await database.Scalar<string>("SELECT ParsedContent FROM FileAdds WHERE FileAddsId=1"), Is.EqualTo("preserved attachment"));
        Assert.That(await database.Scalar<long>("SELECT COUNT(*) FROM SyncChanges"), Is.Zero);
        Assert.That(await database.Scalar<long>("SELECT COALESCE(MAX(Version),0) FROM SyncAccounts"), Is.Zero);
    }

    [Test, Explicit(RequiresMySql)]
    public async Task Mysql_account_lock_blocks_its_writer_but_not_other_accounts()
    {
        await using var database = await MySqlTestDatabase.Create();
        foreach (var id in new[] {1, 2})
        {
            await using var scope = database.Auth();
            await new DataSyncService(scope).Apply(id, History("initial"), default);
        }
        await using var connection = database.Connection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var command = new MySqlCommand("SELECT Version FROM SyncAccounts WHERE AccountId=1 FOR UPDATE", connection, transaction))
            await command.ExecuteScalarAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        async Task<long> Write(int id)
        {
            await using var scope = database.Auth();
            return await new DataSyncService(scope).Apply(id, History("after lock"), timeout.Token);
        }
        var blocked = Write(1);
        try
        {
            Assert.That(await Write(2).WaitAsync(TimeSpan.FromSeconds(10)), Is.EqualTo(2));
            Assert.That(blocked.IsCompleted, Is.False);
        }
        finally { await transaction.RollbackAsync(); }
        Assert.That(await blocked, Is.EqualTo(2));
    }

    [Test, Explicit(RequiresMySql)]
    public async Task Mysql_reused_scope_observes_another_connections_latest_version()
    {
        await using var database = await MySqlTestDatabase.Create();
        await using var original = database.Auth();
        var sync = new DataSyncService(original);
        await sync.Read(1, 0, default);
        await using (var other = database.Auth())
            Assert.That(await new DataSyncService(other).Apply(1, History("other writer"), default), Is.EqualTo(1));
        Assert.That(await sync.Apply(1, History("original writer"), default), Is.EqualTo(2));
    }

    private static SyncMutation History(string title) => new(Guid.NewGuid(), "history", "upsert", "100", Payload: JObject.FromObject(new {title}));
}
