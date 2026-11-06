using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using RelaxKon.Auth.Bootstrap;
using RelaxKon.Auth.Data.Contexts;
using RelaxKon.Auth.Models.Accounts;
using RelaxKon.Auth.Security;
using RelaxKon.Auth.Security.PasswordReset;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace RelaxKon.Auth.Tests;

public class PasswordResetDatabaseTests
{
    [Test, Explicit("Requires TURBO_TEST_MYSQL; creates and removes its own isolated temporary database.")]
    public async Task Sql_admin_activation_revokes_sessions_and_password_reset_is_atomic()
    {
        var configured = Environment.GetEnvironmentVariable("TURBO_TEST_MYSQL");
        if (string.IsNullOrWhiteSpace(configured)) Assert.Ignore("Set TURBO_TEST_MYSQL for the isolated MySQL integration test.");
        var settings = new MySqlConnectionStringBuilder(configured!) { Database = "", AllowUserVariables = true };
        await using var connection = new MySqlConnection(settings.ConnectionString);
        await connection.OpenAsync();
        // Never operate on the configured database. This name is generated exclusively for this test.
        var temporaryDatabase = "turbo_reset_test_" + Guid.NewGuid().ToString("N");
        var created = false;
        try
        {
            await using (var create = new MySqlCommand($"CREATE DATABASE `{temporaryDatabase}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci", connection))
                await create.ExecuteNonQueryAsync();
            created = true;
            await connection.ChangeDatabaseAsync(temporaryDatabase);
            foreach (var resource in new[] { "Schema", "Seed" })
            {
                await using var command = new MySqlCommand(DatabaseBootstrapper.ReadScript(resource), connection);
                await command.ExecuteNonQueryAsync();
            }
            using var stream = typeof(PasswordResetDatabaseTests).Assembly.GetManifestResourceStream("CreateAdmin.sql")!;
            using var reader = new StreamReader(stream);
            var adminSql = (await reader.ReadToEndAsync()).Replace("SET @admin_email = 'REPLACE_WITH_YOUR_REAL_EMAIL';", "SET @admin_email = 'admin@example.com';");
            await using (var command = new MySqlCommand(adminSql, connection)) await command.ExecuteNonQueryAsync();
            settings.Database = temporaryDatabase;
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["ConnectionStrings:ciko"] = settings.ConnectionString }).Build();
            AuthContext Context() => new(configuration);
            var cache = new MemoryDistributedCache(OptionsFactory.Create(new MemoryDistributedCacheOptions()));
            var passwords = new AccountPasswordService();
            var tokens = new ResetTokenService(new EphemeralDataProtectionProvider());
            await using var context = Context();
            var account = await context.Accounts!.AsNoTracking().SingleAsync();
            Assert.That(account.Password, Is.Empty);
            Assert.That(passwords.Verify(account, "anything"), Is.EqualTo(PasswordVerificationState.Invalid));
            var sessionId = Guid.NewGuid();
            context.RefreshTokens!.Add(new RefreshToken {RefreshTokenId = Guid.NewGuid(), AccountId = account.AccountId,
                SessionId = sessionId, TokenHash = new string('a', 64), CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(1)});
            await context.SaveChangesAsync();
            await cache.SetStringAsync("auth:security-stamp:" + account.AccountId, account.SecurityStamp);
            await cache.SetStringAsync("auth:session:" + sessionId.ToString("N"), "1");
            var token = tokens.Read(tokens.Create(account))!;
            var store = new PasswordResetStore(context, passwords, cache);
            Assert.That(await store.ReplacePasswordAsync(token, "NewPassword123!", default), Is.True);
            Assert.That(await store.ReplacePasswordAsync(token, "ReusedPassword!", default), Is.False);
            var updated = await context.Accounts!.AsNoTracking().SingleAsync();
            Assert.That(passwords.Verify(updated, "NewPassword123!"), Is.EqualTo(PasswordVerificationState.Valid));
            Assert.That(updated.SecurityStamp, Is.Not.EqualTo(account.SecurityStamp));
            Assert.That(await context.RefreshTokens!.AsNoTracking().AllAsync(t => t.RevokedAt != null), Is.True);
            Assert.That(await context.AccountRoles!.CountAsync(), Is.EqualTo(3));
            Assert.That(await cache.GetStringAsync("auth:security-stamp:" + account.AccountId), Is.Null);
            Assert.That(await cache.GetStringAsync("auth:session:" + sessionId.ToString("N")), Is.Null);
            // Separate contexts simulate two clients consuming the same link at the same time.
            var identity = tokens.Read(tokens.Create(updated))!;
            await using var left = Context(); await using var right = Context();
            var outcomes = await Task.WhenAll(
                new PasswordResetStore(left, passwords, cache).ReplacePasswordAsync(identity, "ConcurrentOne123!", default),
                new PasswordResetStore(right, passwords, cache).ReplacePasswordAsync(identity, "ConcurrentTwo123!", default));
            Assert.That(outcomes.Count(success => success), Is.EqualTo(1));
            var beforeRepeat = await context.Accounts!.AsNoTracking().SingleAsync();
            await using (var command = new MySqlCommand(adminSql, connection)) await command.ExecuteNonQueryAsync();
            var afterRepeat = await context.Accounts!.AsNoTracking().SingleAsync();
            Assert.That(afterRepeat.Password, Is.EqualTo(beforeRepeat.Password));
            Assert.That(await context.AccountRoles!.CountAsync(), Is.EqualTo(3));
        }
        finally
        {
            if (created)
            {
                await using var cleanup = new MySqlCommand($"DROP DATABASE `{temporaryDatabase}`", connection);
                await cleanup.ExecuteNonQueryAsync();
            }
        }
    }
}
