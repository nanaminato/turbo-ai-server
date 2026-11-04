using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Auth.Contracts.Auth;
using Turbo.Auth.Data.Contexts;
using Turbo.Auth.Models.Accounts;
using Turbo.Auth.Models.Config;
using Turbo.Auth.Security;
using Turbo.Auth.Security.PasswordReset;
using Turbo.Auth.Security.Registration;

namespace Turbo.Auth.Tests;

public class RegistrationTests
{
    private SqliteConnection _connection = null!;
    private AuthContext _database = null!;
    private RegistrationService _service = null!;
    private Sender _sender = null!;
    private Clock _clock = null!;
    private RegistrationOptions _options = null!;
    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:"); await _connection.OpenAsync();
        _connection.CreateCollation("utf8mb4_0900_ai_ci", StringComparer.OrdinalIgnoreCase.Compare);
        _database = new AuthContext(new DbContextOptionsBuilder<AuthContext>().UseSqlite(_connection).Options, new ConfigurationBuilder().Build());
        await _database.Database.EnsureCreatedAsync();
        _sender = new Sender(); _clock = new Clock(); _options = new RegistrationOptions();
        _service = new RegistrationService(_database, new AccountPasswordService(), _sender, Microsoft.Extensions.Options.Options.Create(_options),
            Microsoft.Extensions.Options.Options.Create(new PasswordResetOptions { SmtpHost = "smtp.invalid", FromAddress = "no-reply@example.invalid" }),
            Microsoft.Extensions.Options.Options.Create(new JwtSettings { RefreshTokenPepper = "test-only-registration-pepper" }), _clock, NullLogger<RegistrationService>.Instance);
    }
    [TearDown] public async Task Dispose() { await _database.DisposeAsync(); await _connection.DisposeAsync(); }
    private RegisterRequest Request(string? code = null, string email = "user@example.invalid", string username = "new-user") =>
        new() { Email = email, Username = username, Password = "Password123!", Confirm = "Password123!", EmailCode = code ?? _sender.Code };
    [Test]
    public async Task Verification_is_required_before_any_account_or_role_is_created()
    {
        Assert.That(await _service.Register(Request("123456"), default), Is.EqualTo(RegistrationStatus.InvalidCode));
        Assert.That(await _database.Accounts!.CountAsync(), Is.Zero);
        Assert.That((await _service.SendCode(" User@Example.Invalid ", default)).Status, Is.EqualTo(EmailCodeStatus.Sent));
        Assert.That(_sender.Recipient, Is.EqualTo("user@example.invalid"));
        Assert.That(_sender.Code, Does.Match("^[0-9]{6}$"));
        var challenge = await _database.Set<RegistrationEmailChallenge>().SingleAsync();
        Assert.That(challenge.CodeHash, Has.Length.EqualTo(64).And.Not.EqualTo(_sender.Code));
        Assert.That(challenge.EmailHash, Does.Not.Contain("@"));
        Assert.That(await _database.Accounts!.CountAsync(), Is.Zero);
        Assert.That(await _service.Register(Request(), default), Is.EqualTo(RegistrationStatus.Success));
        var account = await _database.Accounts!.Include(account => account.UserRoles)!.ThenInclude(link => link.Role).SingleAsync();
        Assert.That(new AccountPasswordService().Verify(account, "Password123!"), Is.EqualTo(PasswordVerificationState.Valid));
        Assert.That(account.UserRoles!.Select(link => link.Role!.Name), Is.EqualTo(new[] { "user" }));
        Assert.That(await _service.Register(Request(), default), Is.EqualTo(RegistrationStatus.InvalidCode));
        Assert.That(await _database.Accounts!.CountAsync(), Is.EqualTo(1));
    }
    [Test]
    public async Task Cooldown_resend_and_expiry_are_enforced()
    {
        await _service.SendCode("user@example.invalid", default); var previous = _sender.Code!;
        var cooldown = await _service.SendCode("USER@example.invalid", default);
        Assert.That(cooldown.Status, Is.EqualTo(EmailCodeStatus.Cooldown)); Assert.That(cooldown.RetryAfterSeconds, Is.EqualTo(60));
        Assert.That(_sender.Calls, Is.EqualTo(1));
        _clock.Now = _clock.Now.AddSeconds(61); await _service.SendCode("user@example.invalid", default);
        Assert.That(_sender.Code, Is.Not.EqualTo(previous));
        Assert.That((await _database.Set<RegistrationEmailChallenge>().SingleAsync()).AttemptsLeft, Is.EqualTo(5));
        Assert.That(await _service.Register(Request(previous), default), Is.EqualTo(RegistrationStatus.InvalidCode));
        _clock.Now = _clock.Now.AddMinutes(11);
        Assert.That(await _service.Register(Request(), default), Is.EqualTo(RegistrationStatus.InvalidCode));
    }
    [Test]
    public async Task Wrong_codes_have_five_attempts_and_cannot_be_used_for_another_email()
    {
        await _service.SendCode("user@example.invalid", default);
        Assert.That(await _service.Register(Request(email: "other@example.invalid"), default), Is.EqualTo(RegistrationStatus.InvalidCode));
        var wrong = _sender.Code == "000000" ? "111111" : "000000";
        for (var i = 0; i < 5; i++) Assert.That(await _service.Register(Request(wrong), default), Is.EqualTo(RegistrationStatus.InvalidCode));
        Assert.That(await _service.Register(Request(), default), Is.EqualTo(RegistrationStatus.InvalidCode));
        Assert.That((await _database.Set<RegistrationEmailChallenge>().SingleAsync()).AttemptsLeft, Is.Zero);
        Assert.That(await _database.Accounts!.CountAsync(), Is.Zero);
    }
    [Test]
    public async Task Duplicate_username_or_mailbox_is_checked_after_verification()
    {
        _database.Accounts!.Add(new Account { Username = "taken", Email = "existing@example.invalid", Password = "hash" }); await _database.SaveChangesAsync();
        await _service.SendCode("user@example.invalid", default);
        Assert.That(await _service.Register(Request(username: "taken"), default), Is.EqualTo(RegistrationStatus.AccountExists));
        await _service.SendCode("existing@example.invalid", default);
        Assert.That(await _service.Register(Request(email: "existing@example.invalid"), default), Is.EqualTo(RegistrationStatus.AccountExists));
        Assert.That(await _database.Accounts!.CountAsync(), Is.EqualTo(1));
    }
    [Test]
    public async Task Disabled_registration_or_failed_delivery_never_allows_an_unverified_account()
    {
        _options.Enabled = false;
        Assert.That((await _service.SendCode("user@example.invalid", default)).Status, Is.EqualTo(EmailCodeStatus.Unavailable));
        Assert.That(await _service.Register(Request("123456"), default), Is.EqualTo(RegistrationStatus.Unavailable));
        Assert.That(_sender.Calls, Is.Zero);
        _options.Enabled = true; _sender.Fail = true;
        Assert.That((await _service.SendCode("user@example.invalid", default)).Status, Is.EqualTo(EmailCodeStatus.Unavailable));
        Assert.That(await _service.Register(Request(), default), Is.EqualTo(RegistrationStatus.InvalidCode));
        Assert.That(await _database.Accounts!.CountAsync(), Is.Zero);
    }
    private sealed class Sender : IRegistrationEmailSender
    {
        public string? Recipient; public string? Code; public int Calls; public bool Fail;
        public Task SendAsync(string recipient, string code, CancellationToken token)
        {
            Calls++; Recipient = recipient; Code = code; if (Fail) throw new InvalidOperationException("test transport failure"); return Task.CompletedTask;
        }
    }
    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
}
