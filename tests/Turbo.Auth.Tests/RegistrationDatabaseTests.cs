using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Auth.Contracts.Auth;
using Turbo.Auth.Models.Config;
using Turbo.Auth.Security;
using Turbo.Auth.Security.PasswordReset;
using Turbo.Auth.Security.Registration;

namespace Turbo.Auth.Tests;

public class RegistrationDatabaseTests
{
    [Test, Explicit("Requires TURBO_TEST_MYSQL; creates and removes an isolated database.")]
    public async Task Concurrent_issue_and_registration_are_single_use_across_instances()
    {
        await using var database = await MySqlTestDatabase.Create();
        var sender = new Sender();
        RegistrationService Service(Turbo.Auth.Data.Contexts.AuthContext context) => new(context, new AccountPasswordService(), sender,
            Microsoft.Extensions.Options.Options.Create(new RegistrationOptions()),
            Microsoft.Extensions.Options.Options.Create(new PasswordResetOptions { SmtpHost = "smtp.invalid", FromAddress = "no-reply@example.invalid" }),
            Microsoft.Extensions.Options.Options.Create(new JwtSettings { RefreshTokenPepper = "test-only-registration-pepper" }),
            TimeProvider.System, NullLogger<RegistrationService>.Instance);
        var sent = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            await using var context = database.Auth(); return (await Service(context).SendCode("new@example.invalid", default)).Status;
        }));
        Assert.That(sent.Count(status => status == EmailCodeStatus.Sent), Is.EqualTo(1));
        Assert.That(sender.Codes.Count, Is.EqualTo(1));
        var code = sender.Codes.Single();
        var completed = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            await using var context = database.Auth(); return await Service(context).Register(new RegisterRequest
            {
                Username = "new-user",
                Email = "new@example.invalid",
                Password = "Password123!",
                Confirm = "Password123!",
                EmailCode = code
            }, default);
        }));
        Assert.That(completed.Count(status => status == RegistrationStatus.Success), Is.EqualTo(1));
        Assert.That(await database.Scalar<long>("SELECT COUNT(*) FROM Accounts WHERE Username='new-user'"), Is.EqualTo(1));
        Assert.That(await database.Scalar<long>("SELECT COUNT(*) FROM AccountRoles ar JOIN Accounts a ON a.AccountId=ar.AccountId JOIN Roles r ON r.RoleId=ar.RoleId WHERE a.Username='new-user' AND r.Name='user'"), Is.EqualTo(1));
        Assert.That(await database.Scalar<long>("SELECT AttemptsLeft FROM RegistrationEmailChallenges"), Is.Zero);
    }
    [Test, Explicit("Requires TURBO_TEST_MYSQL; creates and removes an isolated database.")]
    public async Task Different_verified_emails_cannot_race_to_create_the_same_collated_username()
    {
        await using var database = await MySqlTestDatabase.Create();
        var sender = new Sender();
        RegistrationService Service(Turbo.Auth.Data.Contexts.AuthContext context) => new(context, new AccountPasswordService(), sender,
            Microsoft.Extensions.Options.Options.Create(new RegistrationOptions()),
            Microsoft.Extensions.Options.Options.Create(new PasswordResetOptions { SmtpHost = "smtp.invalid", FromAddress = "no-reply@example.invalid" }),
            Microsoft.Extensions.Options.Options.Create(new JwtSettings { RefreshTokenPepper = "test-only-registration-pepper" }),
            TimeProvider.System, NullLogger<RegistrationService>.Instance);
        foreach (var email in new[] { "first@example.invalid", "second@example.invalid" })
        {
            await using var context = database.Auth(); await Service(context).SendCode(email, default);
        }
        var results = await Task.WhenAll(new[] { "first@example.invalid", "second@example.invalid" }.Select(async (email) =>
        {
            await using var context = database.Auth(); return await Service(context).Register(new RegisterRequest
            {
                Username = email.StartsWith("first") ? "shared-name" : "SHARED-NAME",
                Email = email,
                EmailCode = sender.ByEmail[email],
                Password = "Password123!",
                Confirm = "Password123!"
            }, default);
        }));
        Assert.That(results.Count(status => status == RegistrationStatus.Success), Is.EqualTo(1));
        Assert.That(results.Count(status => status == RegistrationStatus.AccountExists), Is.EqualTo(1));
        Assert.That(await database.Scalar<long>("SELECT COUNT(*) FROM Accounts WHERE Username='shared-name'"), Is.EqualTo(1));
    }

    [Test, Explicit("Requires TURBO_TEST_MYSQL; creates and removes an isolated database.")]
    public async Task Registration_migration_is_additive_and_required_by_the_configured_startup_check()
    {
        await using var database = await MySqlTestDatabase.Create(legacy: true);
        await using var connection = database.Connection();
        await Turbo.Auth.Bootstrap.ArchitectureSchemaCheck.Validate(connection.ConnectionString, includeRegistration: true);
        await database.Execute("DROP TABLE RegistrationEmailChallenges");
        var error = Assert.ThrowsAsync<InvalidOperationException>(() => Turbo.Auth.Bootstrap.ArchitectureSchemaCheck.Validate(connection.ConnectionString, includeRegistration: true));
        Assert.That(error!.Message, Does.Contain("registration-email-verification.sql"));
        await database.Execute(MySqlTestDatabase.Resource("RegistrationEmail.sql"));
        Assert.That(await database.Scalar<long>("SELECT COUNT(*) FROM Accounts"), Is.EqualTo(2));
        await Turbo.Auth.Bootstrap.ArchitectureSchemaCheck.Validate(connection.ConnectionString, includeRegistration: true);
    }
    private sealed class Sender : IRegistrationEmailSender
    {
        public ConcurrentBag<string> Codes = [];
        public ConcurrentDictionary<string, string> ByEmail = new();
        public Task SendAsync(string recipient, string code, CancellationToken token) { Codes.Add(code); ByEmail[recipient] = code; return Task.CompletedTask; }
    }
}
