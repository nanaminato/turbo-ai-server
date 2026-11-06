using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using RelaxKon.Auth.Bootstrap;
using RelaxKon.Auth.Data;

namespace RelaxKon.Auth.Tests;

public class SqliteBootstrapTests
{
    private string _directory = null!;
    private DatabaseProvider _provider = null!;
    private string DatabasePath => Path.Combine(_directory, "bootstrap.db");
    private static BootstrapAdmin Admin => new("admin", "admin@example.com", "Password123!");

    [SetUp]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), "turbo-sqlite-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _provider = DatabaseProvider.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:ciko"] = $"Data Source={DatabasePath};Pooling=False"
            }).Build());
    }

    [TearDown]
    public void Cleanup() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void Startup_and_create_admin_do_not_create_missing_database()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() => ArchitectureSchemaCheck.Validate(_provider));
        Assert.That(File.Exists(DatabasePath), Is.False);
        Assert.ThrowsAsync<SqliteException>(() => new DatabaseBootstrapper().RunAsync(_provider, Admin, false));
        Assert.That(File.Exists(DatabasePath), Is.False);
    }

    [Test]
    public async Task Initialization_seeds_full_schema_and_admin_without_overwriting_existing_database()
    {
        var bootstrap = new DatabaseBootstrapper();
        var id = await bootstrap.RunAsync(_provider, Admin, true);
        Assert.That(id, Is.EqualTo(1));
        await ArchitectureSchemaCheck.Validate(_provider, includeRegistration: true);
        Assert.ThrowsAsync<InvalidOperationException>(() => bootstrap.RunAsync(_provider, Admin, true));
        Assert.ThrowsAsync<InvalidOperationException>(() => bootstrap.RunAsync(_provider, Admin, false));
        await using var connection = _provider.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM AccountRoles WHERE AccountId=1";
        Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync()), Is.EqualTo(3));
        command.CommandText = "SELECT COUNT(*) FROM Accounts WHERE Username='ADMIN' AND Email='ADMIN@EXAMPLE.COM'";
        Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync()), Is.EqualTo(1));
        command.CommandText = "SELECT COUNT(*) FROM AvailableModels";
        Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync()), Is.EqualTo(9));
        command.CommandText = "PRAGMA journal_mode";
        Assert.That(Convert.ToString(await command.ExecuteScalarAsync()), Is.EqualTo("wal"));
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('GenerateTasks') WHERE name='DateTime'";
        Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync()), Is.EqualTo(1));
        command.CommandText = "INSERT INTO GenerateTasks (TaskId,AccountId,DateTime) VALUES ('schema-check',1,'2026-11-06 12:00:00')";
        await command.ExecuteNonQueryAsync();
        command.CommandText = "SELECT COUNT(*) FROM pragma_index_info('IX_GenerateTasks_AccountId_DateTime_TaskId') WHERE name='DateTime'";
        Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync()), Is.EqualTo(1));
        command.CommandText = "PRAGMA foreign_keys";
        Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync()), Is.EqualTo(1));
        command.CommandText = "DELETE FROM Accounts WHERE AccountId=1; SELECT COUNT(*) FROM AccountRoles";
        Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync()), Is.Zero);
    }

    [Test]
    public async Task New_sqlite_file_is_owner_only_on_unix()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Unix file modes do not apply on Windows.");
            return;
        }
        await new DatabaseBootstrapper().RunAsync(_provider, Admin, true);
        Assert.That(File.GetUnixFileMode(DatabasePath), Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
    }

    [Test]
    public async Task Initialization_refuses_unrelated_existing_schema()
    {
        await using (var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            // The underscore in SQLite's reserved prefix must not be a SQL LIKE wildcard.
            command.CommandText = "CREATE TABLE sqliteApplicationData (Id INTEGER PRIMARY KEY)";
            await command.ExecuteNonQueryAsync();
        }
        Assert.ThrowsAsync<InvalidOperationException>(() => new DatabaseBootstrapper().RunAsync(_provider, Admin, true));
        await using var verify = _provider.CreateConnection();
        await verify.OpenAsync();
        await using var query = verify.CreateCommand();
        query.CommandText = "SELECT COUNT(*) FROM sqlite_schema WHERE type='table' AND name='Accounts'";
        Assert.That(Convert.ToInt64(await query.ExecuteScalarAsync()), Is.Zero);
        query.CommandText = "PRAGMA journal_mode";
        Assert.That(Convert.ToString(await query.ExecuteScalarAsync()), Is.EqualTo("delete"));
    }

    [Test]
    public async Task Startup_refuses_non_wal_database_without_changing_journal_mode()
    {
        await new DatabaseBootstrapper().RunAsync(_provider, Admin, true);
        await using (var connection = _provider.CreateConnection())
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=DELETE";
            Assert.That(Convert.ToString(await command.ExecuteScalarAsync()), Is.EqualTo("delete"));
        }
        var error = Assert.ThrowsAsync<InvalidOperationException>(() => ArchitectureSchemaCheck.Validate(_provider));
        Assert.That(error!.Message, Does.Contain("WAL"));
        Assert.ThrowsAsync<InvalidOperationException>(() => new DatabaseBootstrapper().RunAsync(_provider, Admin, true));
        await using var verify = _provider.CreateConnection();
        await verify.OpenAsync();
        await using var query = verify.CreateCommand();
        query.CommandText = "PRAGMA journal_mode";
        Assert.That(Convert.ToString(await query.ExecuteScalarAsync()), Is.EqualTo("delete"));
    }

    [Test]
    public async Task Startup_rejects_incomplete_schema_without_repairing_it()
    {
        await new DatabaseBootstrapper().RunAsync(_provider, Admin, true);
        await using var connection = _provider.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP TRIGGER routing_Models_update";
        await command.ExecuteNonQueryAsync();
        var error = Assert.ThrowsAsync<InvalidOperationException>(() => ArchitectureSchemaCheck.Validate(_provider));
        Assert.That(error!.Message, Does.Contain("routing_Models_update"));
        command.CommandText = "SELECT COUNT(*) FROM sqlite_schema WHERE name='routing_Models_update'";
        Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync()), Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Startup_rejects_quota_table_without_unconditional_primary_or_unique_key(bool partialUniqueIndex)
    {
        await new DatabaseBootstrapper().RunAsync(_provider, Admin, true);
        await using var connection = _provider.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DROP TABLE FeatureAccessUsage;
            CREATE TABLE FeatureAccessUsage (
                AccountId INTEGER NOT NULL, FeatureKey TEXT NOT NULL,
                MinuteWindow INTEGER NOT NULL DEFAULT 0, DayWindow INTEGER NOT NULL DEFAULT 0,
                MinuteCount INTEGER NOT NULL DEFAULT 0, DayCount INTEGER NOT NULL DEFAULT 0);
            """;
        await command.ExecuteNonQueryAsync();
        if (partialUniqueIndex)
        {
            command.CommandText = "CREATE UNIQUE INDEX PartialQuotaKey ON FeatureAccessUsage (AccountId,FeatureKey) WHERE AccountId>100";
            await command.ExecuteNonQueryAsync();
        }
        var error = Assert.ThrowsAsync<InvalidOperationException>(() => ArchitectureSchemaCheck.Validate(_provider));
        Assert.That(error!.Message, Does.Contain("FeatureAccessUsage unique(AccountId,FeatureKey)"));
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('FeatureAccessUsage') WHERE pk>0";
        Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync()), Is.Zero);
        command.CommandText = "SELECT COUNT(*) FROM pragma_index_list('FeatureAccessUsage') WHERE [unique]=1 AND partial=0";
        Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync()), Is.Zero);
    }

    [Test]
    public async Task Every_catalog_write_invalidates_routing_revision()
    {
        await new DatabaseBootstrapper().RunAsync(_provider, Admin, true);
        await using var connection = _provider.CreateConnection();
        await connection.OpenAsync();
        async Task<long> Version()
        {
            await using var query = connection.CreateCommand();
            query.CommandText = "SELECT Version FROM RoutingConfiguration WHERE Id=1";
            return Convert.ToInt64(await query.ExecuteScalarAsync());
        }
        foreach (var sql in new[]
        {
            "INSERT INTO Models (Name,IsChatModel,ModelValue,Vision) VALUES ('test',1,'test',0)",
            "UPDATE Models SET Name='updated' WHERE ModelId=1",
            "INSERT INTO SupplierKeys (BaseUrl,RequestIdentifier,Enable,ApiKey) VALUES ('https://example.com',0,1,'test')",
            "UPDATE SupplierKeys SET Enable=0 WHERE SupplierKeyId=1",
            "INSERT INTO ModelKeyBinds (Enable,SupplierKeyId,ModelId,Fee,ProviderModelValue,Priority) VALUES (1,1,1,0,'test',0)",
            "UPDATE ModelKeyBinds SET Priority=1 WHERE ModelKeyBindId=1",
            "DELETE FROM ModelKeyBinds WHERE ModelKeyBindId=1",
            "DELETE FROM SupplierKeys WHERE SupplierKeyId=1",
            "DELETE FROM Models WHERE ModelId=1",
            "INSERT INTO AvailableModels (Enable,Name,IsChatModel,ModelValue,Vision) VALUES (1,'test',1,'test',0)",
            "UPDATE AvailableModels SET Enable=0 WHERE ModelValue='test'",
            "DELETE FROM AvailableModels WHERE ModelValue='test'"
        })
        {
            var before = await Version();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
            Assert.That(await Version(), Is.EqualTo(before + 1), sql);
        }
    }
}
