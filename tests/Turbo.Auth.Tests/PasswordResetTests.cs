using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Auth.Controllers.Auth;
using Turbo.Auth.Models.Accounts;
using Turbo.Auth.Security;
using Turbo.Auth.Security.PasswordReset;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Turbo.Auth.Tests;

public class PasswordResetTests
{
    private EphemeralDataProtectionProvider _protection = null!;
    private ResetTokenService _tokens = null!;
    private FakeStore _store = null!;
    private FakeSender _sender = null!;
    private PasswordResetService _service = null!;

    [SetUp]
    public void Setup()
    {
        _protection = new EphemeralDataProtectionProvider();
        _tokens = new ResetTokenService(_protection);
        _store = new FakeStore(); _sender = new FakeSender();
        _service = new PasswordResetService(_store, _tokens, _sender,
            new MemoryDistributedCache(OptionsFactory.Create(new MemoryDistributedCacheOptions())),
            OptionsFactory.Create(new PasswordResetOptions { Enabled = true, PublicBaseUrl = "https://app.example.com" }),
            NullLogger<PasswordResetService>.Instance);
    }

    [Test]
    public async Task Empty_password_admin_can_set_a_hashed_password_and_links_cannot_be_reused()
    {
        Assert.That(new AccountPasswordService().Verify(_store.Account, "anything"), Is.EqualTo(PasswordVerificationState.Invalid));
        await _service.RequestAsync("admin", "admin@example.com", default);
        Assert.That(_sender.Link, Does.StartWith("https://app.example.com/accounts/forgot-me-password#token="));
        var token = Uri.UnescapeDataString(new Uri(_sender.Link!).Fragment[7..]);
        Assert.That(await _service.ResetAsync(token, "NewPassword123!", default), Is.True);
        Assert.That(new AccountPasswordService().Verify(_store.Account, "NewPassword123!"), Is.EqualTo(PasswordVerificationState.Valid));
        Assert.That(await _service.ResetAsync(token, "AnotherPass123!", default), Is.False);
        Assert.That(_store.Updates, Is.EqualTo(1));
    }

    [Test]
    public async Task Unknown_account_and_mailbox_mismatch_do_not_send_mail()
    {
        await _service.RequestAsync("admin", "wrong@example.com", default);
        await _service.RequestAsync("unknown", "admin@example.com", default);
        Assert.That(_sender.Sent, Is.Zero);
    }

    [Test]
    public async Task Repeated_request_has_a_cooldown()
    {
        await _service.RequestAsync("admin", "admin@example.com", default);
        await _service.RequestAsync("admin", "admin@example.com", default);
        Assert.That(_sender.Sent, Is.EqualTo(1));
    }

    [Test]
    public async Task Delivery_failure_does_not_reveal_account_existence()
    {
        _sender.Fail = true;
        Assert.DoesNotThrowAsync(() => _service.RequestAsync("admin", "admin@example.com", default));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Tampered_expired_and_wrong_purpose_tokens_cannot_change_passwords()
    {
        var token = _tokens.Create(_store.Account);
        Assert.That(await _service.ResetAsync(token.Insert(token.Length / 2, "!"), "NewPassword123!", default), Is.False);
        var payload = JsonSerializer.Serialize(new ResetIdentity(_store.Account.AccountId, _store.Account.Email!,
            _store.Account.SecurityStamp, ResetTokenService.Fingerprint(_store.Account.Password)));
        var expired = _protection.CreateProtector("Turbo.Auth.PasswordReset.v1").ToTimeLimitedDataProtector()
            .Protect(payload, DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.That(await _service.ResetAsync(expired, "NewPassword123!", default), Is.False);
        var wrongPurpose = _protection.CreateProtector("other-purpose").Protect(payload);
        Assert.That(await _service.ResetAsync(wrongPurpose, "NewPassword123!", default), Is.False);
        Assert.That(_store.Updates, Is.Zero);
    }

    [Test]
    public async Task Changing_email_password_or_security_stamp_invalidates_outstanding_links()
    {
        foreach (var field in new[] { "email", "password", "stamp" })
        {
            Setup();
            var token = _tokens.Create(_store.Account);
            if (field == "email") _store.Account.Email = "another@example.com";
            if (field == "password") _store.Account.Password = "changed";
            if (field == "stamp") _store.Account.SecurityStamp = Guid.NewGuid().ToString("N");
            Assert.That(await _service.ResetAsync(token, "NewPassword123!", default), Is.False);
        }
    }

    [TestCase("short")]
    [TestCase("123456789012345678901")]
    [TestCase("        ")]
    public async Task Invalid_passwords_do_not_consume_the_link(string password)
    {
        var token = _tokens.Create(_store.Account);
        Assert.That(await _service.ResetAsync(token, password, default), Is.False);
        Assert.That(await _service.ResetAsync(token, "NewPassword123!", default), Is.True);
    }

    [TestCase("https://app.example.com", true)]
    [TestCase("http://localhost:4200", true)]
    [TestCase("http://app.example.com", false)]
    [TestCase("https://app.example.com?redirect=evil", false)]
    [TestCase("https://user:pass@app.example.com", false)]
    public void Mail_link_origin_must_be_trusted_configuration(string origin, bool valid)
    {
        Assert.That(new PasswordResetOptions {Enabled = true, PublicBaseUrl = origin, SmtpHost = "smtp.example.com",
            FromAddress = "no-reply@example.com"}.IsValid(), Is.EqualTo(valid));
    }

    [Test]
    public async Task Disabled_email_reset_returns_unavailable_without_contacting_the_mailbox()
    {
        var controller = new PasswordResetController(_service, OptionsFactory.Create(new PasswordResetOptions()));
        var result = (ObjectResult)await controller.RequestReset(new ResetRequest {Username = "admin", Email = "admin@example.com"}, default);
        Assert.That(result.StatusCode, Is.EqualTo(503)); Assert.That(_sender.Sent, Is.Zero);
    }

    [TestCase(465, true, MailKit.Security.SecureSocketOptions.SslOnConnect)]
    [TestCase(587, true, MailKit.Security.SecureSocketOptions.StartTls)]
    [TestCase(25, true, MailKit.Security.SecureSocketOptions.StartTls)]
    [TestCase(25, false, MailKit.Security.SecureSocketOptions.None)]
    public void Smtp_transport_uses_the_required_tls_protocol(int port, bool ssl, MailKit.Security.SecureSocketOptions expected)
    {
        Assert.That(PasswordResetEmailSender.SocketOptions(new PasswordResetOptions {SmtpPort = port, EnableSsl = ssl}), Is.EqualTo(expected));
    }

    [Test]
    public void Implicit_tls_port_cannot_be_configured_as_cleartext()
    {
        var options = new PasswordResetOptions {Enabled = true, PublicBaseUrl = "https://app.example.com",
            SmtpHost = "smtp.qiye.aliyun.com", SmtpPort = 465, EnableSsl = false, FromAddress = "no-reply@example.com"};
        Assert.That(options.IsValid(), Is.False);
        options.EnableSsl = true;
        Assert.That(options.IsValid(), Is.True);
    }

    private sealed class FakeSender : IPasswordResetEmailSender
    {
        public string? Link; public int Sent; public bool Fail;
        public Task SendAsync(string recipient, string link, CancellationToken cancellationToken)
        {
            if (Fail) throw new InvalidOperationException("test failure");
            Sent++; Link = link; return Task.CompletedTask;
        }
    }

    private sealed class FakeStore : IPasswordResetStore
    {
        public Account Account = new() {AccountId = 7, Username = "admin", Email = "admin@example.com", Password = ""};
        public int Updates;
        public Task<Account?> FindAsync(string username, string email, CancellationToken cancellationToken) =>
            Task.FromResult<Account?>(username == Account.Username && email == Account.Email ? Account : null);
        public Task<bool> ReplacePasswordAsync(ResetIdentity identity, string password, CancellationToken cancellationToken)
        {
            if (!ResetTokenService.Matches(identity, Account)) return Task.FromResult(false);
            Account.Password = new AccountPasswordService().Hash(Account, password);
            Account.SecurityStamp = Guid.NewGuid().ToString("N"); Updates++;
            return Task.FromResult(true);
        }
    }
}
