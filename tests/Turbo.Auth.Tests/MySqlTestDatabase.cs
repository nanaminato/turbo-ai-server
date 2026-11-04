using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Turbo.Auth.Bootstrap;
using Turbo.Auth.Data.Contexts;

namespace Turbo.Auth.Tests;

/// <summary>Only the generated database is used; the configured default database is never selected.</summary>
internal sealed class MySqlTestDatabase : IAsyncDisposable
{
    private readonly MySqlConnection _maintenance;
    private readonly string _name = "turbo_arch_test_" + Guid.NewGuid().ToString("N");
    private bool _created;
    private string _scopedConnection = "";
    private MySqlTestDatabase(MySqlConnection maintenance) => _maintenance = maintenance;

    public static async Task<MySqlTestDatabase> Create(bool legacy = false, bool upgrade = true)
    {
        var configured = Environment.GetEnvironmentVariable("TURBO_TEST_MYSQL");
        if (string.IsNullOrWhiteSpace(configured)) Assert.Ignore("TURBO_TEST_MYSQL is required for isolated MySQL checks.");
        var settings = new MySqlConnectionStringBuilder(configured!)
            { Database = "", AllowUserVariables = true, Pooling = true, MaximumPoolSize = 32 };
        var fixture = new MySqlTestDatabase(new MySqlConnection(settings.ConnectionString));
        try
        {
            await fixture._maintenance.OpenAsync();
            await fixture.Execute($"CREATE DATABASE `{fixture._name}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci");
            fixture._created = true;
            await fixture._maintenance.ChangeDatabaseAsync(fixture._name);
            settings.Database = fixture._name;
            fixture._scopedConnection = settings.ConnectionString;
            TestContext.Progress.WriteLine($"Isolated MySQL {await fixture.Scalar<string>("SELECT VERSION()")}: {(legacy ? "legacy upgrade" : "fresh schema")}");
            await fixture.Execute(legacy ? Resource("PreArchitecture.sql") : DatabaseBootstrapper.ReadScript("Schema"));
            await fixture.Execute("""
                INSERT INTO Accounts(AccountId,Username,Password,Email) VALUES
                  (1,'test-one','hash','one@example.com'),(2,'test-two','hash','two@example.com');
                INSERT INTO ChatHistories(ChatHistoryId,UserId,DataId,Title) VALUES (1,1,100,'legacy history'),(2,2,100,'other account');
                INSERT INTO ChatMessages(ChatMessageId,ChatHistoryId,DataId,Role,Content,ShowType,Finish,Model)
                  VALUES (1,1,200,'user','legacy message',0,1,'test'),(2,2,200,'user','private message',0,1,'test');
                INSERT INTO FileAdds(FileAddsId,ChatMessageId,FileName,FileType,FileSize,FileContent,ParsedContent)
                  VALUES (1,1,'legacy.txt','text/plain',4,'test','preserved attachment');
                INSERT INTO GenerateTasks(TaskId,TaskType,AccountId,DateTime,Images,Videos)
                  VALUES ('legacy-task','image',1,'2026-11-04 00:00:00',JSON_ARRAY('https://example.invalid/image'),JSON_ARRAY());
                """);
            if (legacy && upgrade) await fixture.Execute(Resource("ArchitectureUpgrade.sql"));
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    public static string Resource(string name)
    {
        using var stream = typeof(MySqlTestDatabase).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Missing test SQL resource: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public MySqlConnection Connection() => new(_scopedConnection);

    public AuthContext Auth() => new(new DbContextOptionsBuilder<AuthContext>()
        .UseMySql(_scopedConnection, ServerVersion.Parse("8.4.6-mysql")).Options, Configuration());
    public KeyContext Keys() => new(new DbContextOptionsBuilder<KeyContext>()
        .UseMySql(_scopedConnection, ServerVersion.Parse("8.4.6-mysql")).Options, Configuration());
    private IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["ConnectionStrings:ciko"] = _scopedConnection }).Build();

    public async Task Execute(string sql)
    {
        await using var command = new MySqlCommand(sql, _maintenance) { CommandTimeout = 60 };
        await command.ExecuteNonQueryAsync();
    }
    public async Task<T> Scalar<T>(string sql)
    {
        await using var command = new MySqlCommand(sql, _maintenance);
        return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T));
    }
    public async Task<string[]> Rows(string sql)
    {
        await using var command = new MySqlCommand(sql, _maintenance);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync()) rows.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount)
            .Select(index => reader.IsDBNull(index) ? "<null>" : Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture))));
        return rows.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_created) return;
            // Never drop a supplied/configured database, even on setup failure.
            if (!System.Text.RegularExpressions.Regex.IsMatch(_name, "^turbo_arch_test_[0-9a-f]{32}$"))
                throw new InvalidOperationException("Invalid isolated test database name.");
            using var pool = new MySqlConnection(_scopedConnection);
            MySqlConnection.ClearPool(pool);
            await _maintenance.ChangeDatabaseAsync("information_schema");
            await Execute($"DROP DATABASE `{_name}`");
            _created = false;
            TestContext.Progress.WriteLine("Isolated database removed.");
        }
        finally { await _maintenance.DisposeAsync(); }
    }
}
