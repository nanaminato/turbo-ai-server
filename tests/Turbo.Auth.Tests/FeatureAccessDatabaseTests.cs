using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Turbo.Auth.Bootstrap;
using Turbo.Auth.Security.FeatureAccess;

namespace Turbo.Auth.Tests;

public class FeatureAccessDatabaseTests
{
    [Test, Explicit("Requires TURBO_TEST_MYSQL; creates and drops an isolated test database.")]
    public async Task Distributed_admission_policy_conflicts_and_ownership_are_atomic()
    {
        var configured = Environment.GetEnvironmentVariable("TURBO_TEST_MYSQL");
        if (string.IsNullOrWhiteSpace(configured)) Assert.Ignore("Set TURBO_TEST_MYSQL to run MySQL integration checks.");
        var settings = new MySqlConnectionStringBuilder(configured!) { Database = "", AllowUserVariables = true };
        await using var connection = new MySqlConnection(settings.ConnectionString);
        await connection.OpenAsync();
        var database = "turbo_feature_test_" + Guid.NewGuid().ToString("N");
        var created = false;
        try
        {
            await using (var command = new MySqlCommand($"CREATE DATABASE `{database}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci", connection))
                await command.ExecuteNonQueryAsync();
            created = true;
            await connection.ChangeDatabaseAsync(database);
            await using (var command = new MySqlCommand(DatabaseBootstrapper.ReadScript("Schema"), connection))
                await command.ExecuteNonQueryAsync();
            settings.Database = database;
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["ConnectionStrings:ciko"] = settings.ConnectionString }).Build();
            var stores = new[] { new FeatureAccessStore(configuration), new FeatureAccessStore(configuration) };
            var saves = await Task.WhenAll(stores.Select(store => store.Save(FeaturePolicies.Empty, default)));
            Assert.That(saves.Count(saved => saved), Is.EqualTo(1));
            Assert.That((await stores[0].Read(default)).Revision, Is.EqualTo(1));
            var concurrency = new EffectiveFeature("chat", true, null, null, 1);
            var admissions = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => stores[i%2].Acquire(1, concurrency, default)));
            var admitted = admissions.Single(a => a.LeaseId is not null);
            Assert.That(admissions.Count(a => a.LeaseId is not null), Is.EqualTo(1));
            Assert.That(await stores[1].Renew(admitted.LeaseId!, default), Is.True);
            await stores[1].Release(admitted.LeaseId!, default);
            var next = await stores[0].Acquire(1, concurrency, default);
            Assert.That(next.LeaseId, Is.Not.Null);
            await using (var expire = new MySqlCommand("UPDATE FeatureAccessLeases SET ExpiresAt=DATE_SUB(UTC_TIMESTAMP(),INTERVAL 1 SECOND)", connection))
                await expire.ExecuteNonQueryAsync();
            Assert.That(await stores[0].Renew(next.LeaseId!, default), Is.False);
            Assert.That((await stores[1].Acquire(1, concurrency, default)).LeaseId, Is.Not.Null);
            // Distinct stores and unlimited HTTP concurrency still cannot exceed daily count.
            var daily = new EffectiveFeature("image", true, null, 1, null);
            var counts = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => stores[i%2].Acquire(2, daily, default)));
            Assert.That(counts.Count(a => a.LeaseId is not null), Is.EqualTo(1));
            var rate = new EffectiveFeature("tts", true, 1, null, null);
            var first = await stores[0].Acquire(3, rate, default);
            await stores[0].Release(first.LeaseId!, default);
            Assert.That((await stores[1].Acquire(3, rate, default)).LeaseId, Is.Null);
            await stores[0].RecordTask(1, "provider-task", default);
            Assert.That(await stores[1].OwnsTask(1, "provider-task", default), Is.True);
            Assert.That(await stores[1].OwnsTask(2, "provider-task", default), Is.False);
            Assert.That(await stores[1].OwnsTask(1, "legacy-task", default), Is.False);
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            if (created)
            {
                await connection.ChangeDatabaseAsync("mysql");
                await using var drop = new MySqlCommand($"DROP DATABASE `{database}`", connection);
                await drop.ExecuteNonQueryAsync();
            }
        }
    }
}
