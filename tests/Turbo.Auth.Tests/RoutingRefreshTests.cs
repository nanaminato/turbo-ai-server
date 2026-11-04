using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Data.Contexts;
using Turbo.Auth.Models.Suppliers;

namespace Turbo.Auth.Tests;

public class RoutingRefreshTests
{
    [Test]
    public async Task Two_replicas_observe_a_shared_revision_and_disable_a_key()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation("utf8mb4_0900_ai_ci", StringComparer.OrdinalIgnoreCase.Compare);
        var options = new DbContextOptionsBuilder<KeyContext>().UseSqlite(connection).Options;
        var configuration = new ConfigurationBuilder().Build();
        await using var database = new KeyContext(options, configuration);
        await database.Database.EnsureCreatedAsync();
        await database.Database.ExecuteSqlRawAsync("CREATE TABLE RoutingConfiguration (Id INTEGER PRIMARY KEY, Version INTEGER); INSERT INTO RoutingConfiguration VALUES (1,1);");
        var key = new SupplierKey { Enable = true, BaseUrl = "https://example.invalid", ApiKey = "test" };
        key.ModelKeyBinds = [new ModelKeyBind { Enable = true, SupplierKey = key, Fee = 1,
            Model = new Model { Name = "test", ModelValue = "test" } }];
        database.Add(key);
        await database.SaveChangesAsync();
        var replicas = new[] { new QuickModel(), new QuickModel() };
        var gates = new[] { new RouteReloadGate(TimeProvider.System), new RouteReloadGate(TimeProvider.System) };
        async Task Refresh(int index)
        {
            await using var scope = new KeyContext(options, configuration);
            await new KeyLoader(scope, new StableKeyPoolRepository(new ModelKeyBuilder(), replicas[index]), gates[index]).LoadKeys();
        }
        await Refresh(0); await Refresh(1);
        Assert.That(replicas.All(item => item.IsModelAvailable("test")), Is.True);
        key.Enable = false;
        await database.SaveChangesAsync();
        await database.Database.ExecuteSqlRawAsync("UPDATE RoutingConfiguration SET Version=2 WHERE Id=1");
        await Refresh(0); await Refresh(1);
        Assert.That(replicas.All(item => !item.IsModelAvailable("test")), Is.True);
    }

    [Test]
    public async Task Stale_routes_fail_closed_and_recover_even_at_the_same_revision()
    {
        var clock = new TestClock();
        var gate = new RouteReloadGate(clock) { LoadedRevision = 7 };
        var routes = new QuickModel();
        routes.Transfer(new QuickModel { Quick = new() { ["test"] = [new WeightKey { Weight = 1, SupplierKey = new SupplierKey() }] } });
        gate.Checked();
        clock.Now += TimeSpan.FromSeconds(31);
        await gate.Expire(routes, TimeSpan.FromSeconds(30), default);
        Assert.That(routes.IsModelAvailable("test"), Is.False);
        Assert.That(gate.LoadedRevision, Is.Null);
    }

    private sealed class TestClock : TimeProvider
    { public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
}
