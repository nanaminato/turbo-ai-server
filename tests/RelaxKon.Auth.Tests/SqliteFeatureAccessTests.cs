using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using RelaxKon.Auth.Security.FeatureAccess;

namespace RelaxKon.Auth.Tests;

public class SqliteFeatureAccessTests
{
    private sealed class Database : IAsyncDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), $"turbo-quota-{Guid.NewGuid():N}.db");
        public IConfiguration Configuration(int timeout = 30) => new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> {
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:ciko"] = $"Data Source={path};Mode=ReadWriteCreate;Pooling=False;Default Timeout={timeout}"
            }).Build();
        public async Task Execute(string sql)
        {
            await using var connection = new SqliteConnection(Configuration().GetConnectionString("ciko"));
            await connection.OpenAsync();
            await using var command = connection.CreateCommand(); command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
        public async Task<long> Scalar(string sql)
        {
            await using var connection = new SqliteConnection(Configuration().GetConnectionString("ciko"));
            await connection.OpenAsync();
            await using var command = connection.CreateCommand(); command.CommandText = sql;
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        public static async Task<Database> Create()
        {
            var database = new Database();
            await database.Execute("""
                PRAGMA journal_mode=WAL;
                CREATE TABLE Roles(RoleId INTEGER PRIMARY KEY, Code TEXT UNIQUE, Name TEXT);
                CREATE TABLE AccountRoles(AccountId INTEGER, RoleId INTEGER);
                CREATE TABLE FeatureAccessPolicies(Id INTEGER PRIMARY KEY, Revision INTEGER NOT NULL, Document TEXT NOT NULL);
                CREATE TABLE FeatureAccessUsage(AccountId INTEGER, FeatureKey TEXT, MinuteWindow INTEGER NOT NULL DEFAULT 0,
                    DayWindow INTEGER NOT NULL DEFAULT 0, MinuteCount INTEGER NOT NULL DEFAULT 0, DayCount INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY(AccountId,FeatureKey));
                CREATE TABLE FeatureAccessLeases(LeaseId TEXT PRIMARY KEY, AccountId INTEGER, FeatureKey TEXT, ExpiresAt TEXT NOT NULL);
                CREATE TABLE FeatureAccessTasks(TaskId TEXT PRIMARY KEY, AccountId INTEGER, SupplierKeyId INTEGER);
                INSERT INTO Roles VALUES(1,'admin','Admin'),(2,'user','User'),(3,'vip','VIP');
                INSERT INTO AccountRoles VALUES(1,2),(2,1);
                """);
            return database;
        }
        public ValueTask DisposeAsync()
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
            return ValueTask.CompletedTask;
        }
    }

    [Test]
    public async Task Independent_stores_serialize_concurrent_admission_and_daily_limits()
    {
        await using var database = await Database.Create();
        var stores = Enumerable.Range(0, 8).Select(_ => new FeatureAccessStore(database.Configuration())).ToArray();
        // Task.Run is deliberate: SQLite's async ADO operations complete synchronously, so merely
        // enumerating asynchronous methods would not exercise concurrent independent connections.
        var admissions = await Task.WhenAll(Enumerable.Range(0, 24).Select(i => Task.Run(() =>
            stores[i % stores.Length].Acquire(1, new("chat", true, null, null, 3), default))));
        Assert.That(admissions.Count(x => x.LeaseId != null), Is.EqualTo(3));
        Assert.That(admissions.Where(x => x.LeaseId == null).All(x => x.RetryAfterSeconds > 0), Is.True);
        var daily = await Task.WhenAll(Enumerable.Range(0, 24).Select(i => Task.Run(() =>
            stores[i % stores.Length].Acquire(2, new("image", true, null, 5, null), default))));
        Assert.That(daily.Count(x => x.LeaseId != null), Is.EqualTo(5));
        Assert.That(await database.Scalar("SELECT DayCount FROM FeatureAccessUsage WHERE AccountId=2"), Is.EqualTo(5));
    }

    [Test]
    public async Task Writer_timeout_fails_closed_without_admission_or_counter_changes()
    {
        await using var database = await Database.Create();
        var store = new FeatureAccessStore(database.Configuration(timeout: 1));
        var policy = new EffectiveFeature("chat", true, 1, 1, 1);
        await using (var blocker = new SqliteConnection(database.Configuration().GetConnectionString("ciko")))
        {
            await blocker.OpenAsync();
            await using var held = blocker.BeginTransaction(deferred: false);
            var error = Assert.ThrowsAsync<SqliteException>(() => Task.Run(() => store.Acquire(1, policy, default)));
            Assert.That(error!.SqliteErrorCode, Is.EqualTo(5));
            // A timeout is not an accepted request, and the reader still works in WAL.
            var usage = await new FeatureUsageReader(database.Configuration()).ReadUsage(1, default);
            Assert.That(usage.Features.All(item => item.DayUsed == 0 && item.ActiveRequests == 0), Is.True);
        }
        Assert.That(await database.Scalar("SELECT COUNT(*) FROM FeatureAccessUsage"), Is.Zero);
        Assert.That(await database.Scalar("SELECT COUNT(*) FROM FeatureAccessLeases"), Is.Zero);
        Assert.That((await store.Acquire(1, policy, default)).LeaseId, Is.Not.Null);
    }

    [Test]
    public async Task Leases_cannot_be_resurrected_and_release_does_not_refund_quota()
    {
        await using var database = await Database.Create();
        var first = new FeatureAccessStore(database.Configuration());
        var second = new FeatureAccessStore(database.Configuration());
        var policy = new EffectiveFeature("chat", true, null, 2, 1);
        var admission = await first.Acquire(1, policy, default);
        Assert.That(await second.Renew(admission.LeaseId!, default), Is.True);
        Assert.That((await second.Acquire(1, policy, default)).LeaseId, Is.Null);
        await database.Execute("UPDATE FeatureAccessLeases SET ExpiresAt=datetime('now','-1 second')");
        Assert.That(await second.Renew(admission.LeaseId!, default), Is.False);
        var next = await second.Acquire(1, policy, default);
        Assert.That(next.LeaseId, Is.Not.Null);
        await first.Release(next.LeaseId!, default);
        Assert.That(await first.Renew(next.LeaseId!, default), Is.False);
        Assert.That((await first.Acquire(1, policy, default)).LeaseId, Is.Null);
        await database.Execute("UPDATE FeatureAccessUsage SET MinuteWindow=0,DayWindow=0");
        Assert.That((await first.Acquire(1, policy, default)).LeaseId, Is.Not.Null);
    }

    [Test]
    public async Task Minute_limits_survive_release_and_reset_stale_windows()
    {
        await using var database = await Database.Create();
        var store = new FeatureAccessStore(database.Configuration());
        var policy = new EffectiveFeature("tts", true, 1, null, null);
        var admission = await store.Acquire(1, policy, default);
        await store.Release(admission.LeaseId!, default);
        Assert.That((await store.Acquire(1, policy, default)).LeaseId, Is.Null);
        await database.Execute("UPDATE FeatureAccessUsage SET MinuteWindow=0");
        Assert.That((await store.Acquire(1, policy, default)).LeaseId, Is.Not.Null);
    }

    [Test]
    public async Task Policy_revision_conflicts_are_atomic_and_invalid_roles_are_rejected()
    {
        await using var database = await Database.Create();
        var stores = Enumerable.Range(0, 8).Select(_ => new FeatureAccessStore(database.Configuration())).ToArray();
        var saves = await Task.WhenAll(stores.Select(store => Task.Run(() => store.Save(FeaturePolicies.Empty, default))));
        Assert.That(saves.Count(x => x), Is.EqualTo(1));
        var document = await stores[0].Read(default);
        Assert.That(document.Revision, Is.EqualTo(1));
        document.Features[0].Roles["missing"] = new(true);
        Assert.ThrowsAsync<RelaxKon.Auth.Security.RoleRuleException>(() => stores[1].Save(document, default));
        Assert.That((await stores[0].Read(default)).Revision, Is.EqualTo(1));
    }

    [Test]
    public async Task Task_receipts_fail_closed_for_owner_route_conflicts_and_unknown_tasks()
    {
        await using var database = await Database.Create();
        var first = new FeatureAccessStore(database.Configuration());
        var second = new FeatureAccessStore(database.Configuration());
        await first.RecordTask(1, "receipt", default, 7);
        await second.RecordTask(1, "receipt", default, 7);
        Assert.ThrowsAsync<InvalidOperationException>(() => second.RecordTask(2, "receipt", default, 7));
        Assert.ThrowsAsync<InvalidOperationException>(() => second.RecordTask(1, "receipt", default, 8));
        Assert.ThrowsAsync<InvalidOperationException>(() => second.RecordTask(1, "receipt", default));
        Assert.That(await second.OwnsTask(1, "receipt", default), Is.True);
        Assert.That(await second.OwnsTask(2, "receipt", default), Is.False);
        Assert.That(await second.OwnsTask(1, "unknown", default), Is.False);
        Assert.That(await second.GetTaskSupplierKeyId(1, "receipt", default), Is.EqualTo(7));
        Assert.That(await second.GetTaskSupplierKeyId(2, "receipt", default), Is.Null);
    }

    [Test]
    public async Task Concurrent_receipt_claims_cannot_transfer_ownership()
    {
        await using var database = await Database.Create();
        var claims = await Task.WhenAll(Enumerable.Range(1, 12).Select(account => Task.Run(async () =>
        {
            try
            {
                await new FeatureAccessStore(database.Configuration()).RecordTask(account, "contested", default, account);
                return account;
            }
            catch (InvalidOperationException) { return 0; }
        })));
        var owner = claims.Single(account => account != 0);
        var store = new FeatureAccessStore(database.Configuration());
        Assert.That(await store.OwnsTask(owner, "contested", default), Is.True);
        Assert.That(await store.GetTaskSupplierKeyId(owner, "contested", default), Is.EqualTo(owner));
        Assert.That(await database.Scalar("SELECT COUNT(*) FROM FeatureAccessTasks"), Is.EqualTo(1));
    }

    [Test]
    public async Task Usage_does_not_seed_reset_or_clean_up_and_reads_current_roles()
    {
        await using var database = await Database.Create();
        var reader = new FeatureUsageReader(database.Configuration());
        Assert.That((await reader.ReadUsage(1, default)).Features.All(x => !x.Policy.Enabled), Is.True);
        Assert.That(await database.Scalar("SELECT COUNT(*) FROM FeatureAccessPolicies"), Is.Zero);
        var store = new FeatureAccessStore(database.Configuration());
        var policy = FeaturePolicies.Empty;
        policy.Features[0].Roles["user"] = new(true, 10, 100, 3);
        policy.Features[0].Roles["vip"] = new(true);
        await store.Save(policy, default);
        await store.Acquire(1, FeaturePolicies.Resolve(policy, "chat", ["user"]), default);
        await database.Execute("""
            INSERT INTO FeatureAccessLeases VALUES('expired',1,'chat',datetime('now','-1 second'));
            INSERT INTO FeatureAccessUsage VALUES(1,'image',0,0,9,99);
            """);
        // Reject any attempted feature-table writes at the database level, including no-op
        // updates and cleanup. Usage must succeed using only a deferred read transaction.
        foreach (var table in new[] { "FeatureAccessPolicies", "FeatureAccessUsage", "FeatureAccessLeases", "FeatureAccessTasks" })
            foreach (var operation in new[] { "INSERT", "UPDATE", "DELETE" })
                await database.Execute($"CREATE TRIGGER deny_{table}_{operation} BEFORE {operation} ON {table} BEGIN SELECT RAISE(ABORT,'usage must be read-only'); END;");
        var usage = await reader.ReadUsage(1, default);
        var chat = usage.Features.Single(x => x.Policy.Key == "chat");
        Assert.Multiple(() => {
            Assert.That(usage.PolicyRevision, Is.EqualTo(1));
            Assert.That(chat.DayUsed, Is.EqualTo(1));
            Assert.That(chat.ActiveRequests, Is.EqualTo(1));
            Assert.That(chat.ConcurrentRemaining, Is.EqualTo(2));
            Assert.That(usage.Features.Single(x => x.Policy.Key == "image").DayUsed, Is.Zero);
        });
        Assert.That((await reader.ReadUsage(2, default)).Features.All(x => !x.Policy.Enabled), Is.True);
        Assert.That((await reader.ReadUsage(99, default)).Features.All(x => x.DayUsed == 0), Is.True);
        Assert.That(await database.Scalar("SELECT COUNT(*) FROM FeatureAccessLeases"), Is.EqualTo(2));
        Assert.That(await database.Scalar("SELECT DayCount FROM FeatureAccessUsage WHERE FeatureKey='image'"), Is.EqualTo(99));
        await database.Execute("INSERT INTO AccountRoles VALUES(1,3)");
        Assert.That((await reader.ReadUsage(1, default)).Features[0].DayRemaining, Is.Null);
        await database.Execute("DELETE FROM AccountRoles WHERE AccountId=1");
        Assert.That((await reader.ReadUsage(1, default)).Features[0].DayRemaining, Is.Zero);
    }
}
