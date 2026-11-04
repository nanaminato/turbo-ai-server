using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Newtonsoft.Json.Linq;
using Turbo.Auth.Application.Sync;
using Turbo.Auth.Bootstrap;
using Turbo.Auth.Data.Contexts;

namespace Turbo.Auth.Tests;

public class ArchitectureDatabaseTests
{
    [Test, Explicit("Requires TURBO_TEST_MYSQL; creates and drops an isolated test database.")]
    public async Task Mysql_schema_triggers_and_parallel_sync_commits_are_consistent()
    {
        var configured = Environment.GetEnvironmentVariable("TURBO_TEST_MYSQL");
        if (string.IsNullOrWhiteSpace(configured)) Assert.Ignore("Set TURBO_TEST_MYSQL for MySQL integration checks.");
        var settings = new MySqlConnectionStringBuilder(configured!) {Database = "", AllowUserVariables = true};
        await using var connection = new MySqlConnection(settings.ConnectionString);
        await connection.OpenAsync();
        var name = "turbo_arch_test_" + Guid.NewGuid().ToString("N");
        var created = false;
        try
        {
            await using (var create = new MySqlCommand($"CREATE DATABASE `{name}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci", connection))
                await create.ExecuteNonQueryAsync();
            created = true;
            await connection.ChangeDatabaseAsync(name);
            await using (var schema = new MySqlCommand(DatabaseBootstrapper.ReadScript("Schema"), connection))
                await schema.ExecuteNonQueryAsync();
            await using (var seed = new MySqlCommand("INSERT INTO Accounts(AccountId,Username,Password,Email) VALUES (1,'test','hash','test@example.com')", connection))
                await seed.ExecuteNonQueryAsync();
            settings.Database = name;
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["ConnectionStrings:ciko"] = settings.ConnectionString }).Build();
            AuthContext Open() => new(new DbContextOptionsBuilder<AuthContext>()
                .UseMySql(settings.ConnectionString, ServerVersion.Parse("8.4.6-mysql")).Options, configuration);
            var mutation = new SyncMutation(Guid.NewGuid(), "history", "upsert", "100", Payload: JObject.FromObject(new {title = "test"}));
            var revisions = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
            {
                await using var scope = Open();
                return await new DataSyncService(scope).Apply(1, mutation, default);
            }));
            Assert.That(revisions, Is.All.EqualTo(1));
            await Task.WhenAll(Enumerable.Range(1, 12).Select(async index =>
            {
                await using var scope = Open();
                await new DataSyncService(scope).Apply(1, new(Guid.NewGuid(), "message", "upsert", index.ToString(), 100,
                    JObject.FromObject(new {role = "user", content = $"message{index}", finish = true})), default);
            }));
            await using (var verification = Open())
            {
                var changes = await new DataSyncService(verification).Read(1, 1, default);
                Assert.That(changes.Changes.Select(item => item.Version), Is.EqualTo(Enumerable.Range(2, 12).Select(value => (long)value)));
                Assert.That(changes.Cursor, Is.EqualTo(13));
                Assert.That(await verification.ChatHistories!.CountAsync(), Is.EqualTo(1));
            }
            await using (var model = new MySqlCommand("INSERT INTO Models(Name,IsChatModel,ModelValue,Vision) VALUES ('test',1,'test',0)", connection))
                await model.ExecuteNonQueryAsync();
            await using (var revision = new MySqlCommand("SELECT Version FROM RoutingConfiguration WHERE Id=1", connection))
                Assert.That(Convert.ToInt64(await revision.ExecuteScalarAsync()), Is.GreaterThan(1));
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            if (created)
            {
                await connection.ChangeDatabaseAsync("mysql");
                await using var drop = new MySqlCommand($"DROP DATABASE `{name}`", connection);
                await drop.ExecuteNonQueryAsync();
            }
        }
    }
}
