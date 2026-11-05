using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Turbo.Auth.Contracts.Auth;
using Turbo.Auth.Data.Contexts;
using Turbo.Auth.Models.Accounts;
using Turbo.Auth.Models.Config;
using Turbo.Auth.Security.PasswordReset;

namespace Turbo.Auth.Security.Registration;

public enum RegistrationStatus { Success, InvalidCode, AccountExists, Unavailable }
public enum EmailCodeStatus { Sent, Cooldown, Unavailable }
public sealed record EmailCodeResult(EmailCodeStatus Status, int RetryAfterSeconds = 0);
public interface IRegistrationService
{
    Task<EmailCodeResult> SendCode(string email, CancellationToken token);
    Task<RegistrationStatus> Register(RegisterRequest request, CancellationToken token);
}
public sealed class RegistrationService(AuthContext database, IAccountPasswordService passwords, IRegistrationEmailSender sender,
    IOptions<RegistrationOptions> options, IOptions<PasswordResetOptions> smtp, IOptions<JwtSettings> jwt,
    TimeProvider time, ILogger<RegistrationService> logger) : IRegistrationService
{
    private bool Available => options.Value.Enabled && smtp.Value.IsSmtpValid();
    private bool MySql => database.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true;
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
    private static string EmailKey(string email) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email)));
    private string Hash(string email, string code) => Convert.ToHexString(HMACSHA256.HashData(
        Encoding.UTF8.GetBytes(jwt.Value.RefreshTokenPepper!), Encoding.UTF8.GetBytes("registration-email-v1\n" + email + "\n" + code)));
    private async Task<RegistrationEmailChallenge> Lock(string key, DateTime now, CancellationToken token)
    {
        if (MySql)
            await database.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO RegistrationEmailChallenges (EmailHash,CodeHash,ExpiresAt,NextSendAt,AttemptsLeft) VALUES ({key},{string.Empty},{now},{now},0) ON DUPLICATE KEY UPDATE EmailHash=EmailHash", token);
        else
            await database.Database.ExecuteSqlInterpolatedAsync($"INSERT OR IGNORE INTO RegistrationEmailChallenges (EmailHash,CodeHash,ExpiresAt,NextSendAt,AttemptsLeft) VALUES ({key},{string.Empty},{now},{now},0)", token);
        var challenge = MySql
            ? (await database.Set<RegistrationEmailChallenge>().FromSqlInterpolated($"SELECT * FROM RegistrationEmailChallenges WHERE EmailHash={key} FOR UPDATE").ToListAsync(token)).Single()
            : await database.Set<RegistrationEmailChallenge>().SingleAsync(item => item.EmailHash == key, token);
        // Refresh an identity-map entry when a context is reused after another instance changed this row.
        await database.Entry(challenge).ReloadAsync(token);
        return challenge;
    }
    public async Task<EmailCodeResult> SendCode(string email, CancellationToken token)
    {
        if (!Available) return new(EmailCodeStatus.Unavailable);
        email = Normalize(email);
        var now = time.GetUtcNow().UtcDateTime;
        var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var hash = Hash(email, code);
        var key = EmailKey(email);
        await using (var transaction = await database.Database.BeginTransactionAsync(MySql ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, token))
        {
            var challenge = await Lock(key, now, token);
            if (challenge.NextSendAt > now)
                return new(EmailCodeStatus.Cooldown, Math.Max(1, (int)Math.Ceiling((challenge.NextSendAt - now).TotalSeconds)));
            // A resend must invalidate the preceding code even in the rare event of an RNG repeat.
            while (challenge.CodeHash == hash)
            {
                code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
                hash = Hash(email, code);
            }
            challenge.CodeHash = hash; challenge.ExpiresAt = now.AddMinutes(10);
            challenge.NextSendAt = now.AddSeconds(60); challenge.AttemptsLeft = 5;
            await database.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }
        try { await sender.SendAsync(email, code, token); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Registration email delivery failed. ErrorType: {ErrorType}", error.GetType().Name);
            await database.Set<RegistrationEmailChallenge>().Where(item => item.EmailHash == key && item.CodeHash == hash)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.AttemptsLeft, 0), token);
            return new(EmailCodeStatus.Unavailable);
        }
        return new(EmailCodeStatus.Sent);
    }
    public async Task<RegistrationStatus> Register(RegisterRequest request, CancellationToken token)
    {
        if (!Available) return RegistrationStatus.Unavailable;
        var email = Normalize(request.Email!);
        var username = request.Username!.Trim();
        if (username.Length is < 1 or > 20 || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length is < 6 or > 128 || request.Password != request.Confirm ||
            request.EmailCode is null || !System.Text.RegularExpressions.Regex.IsMatch(request.EmailCode, "^[0-9]{6}$"))
            return RegistrationStatus.InvalidCode;
        string? lockName = null;
        if (MySql) await database.Database.OpenConnectionAsync(token);
        try
        {
            if (MySql)
            {
                // Serialize public registrations across instances, including collated legacy username/email checks.
                var connection = (MySqlConnection)database.Database.GetDbConnection();
                var name = "turbo-register-" + EmailKey(connection.Database.ToLowerInvariant())[..40];
                await using var acquire = new MySqlCommand("SELECT GET_LOCK(@name, 5)", connection);
                acquire.Parameters.AddWithValue("@name", name);
                if (Convert.ToInt32(await acquire.ExecuteScalarAsync(token)) != 1) return RegistrationStatus.Unavailable;
                lockName = name;
            }
            await using var transaction = await database.Database.BeginTransactionAsync(MySql ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, token);
            var now = time.GetUtcNow().UtcDateTime;
            var challenge = await Lock(EmailKey(email), now, token);
            if (challenge.AttemptsLeft <= 0 || challenge.ExpiresAt <= now) return RegistrationStatus.InvalidCode;
            var expected = Hash(email, request.EmailCode);
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(challenge.CodeHash)))
            {
                challenge.AttemptsLeft--;
                await database.SaveChangesAsync(token); await transaction.CommitAsync(token);
                return RegistrationStatus.InvalidCode;
            }
            if (await database.Accounts!.AnyAsync(account => account.Username == username || account.Email == email, token))
                return RegistrationStatus.AccountExists;
            var role = await database.Roles!.SingleOrDefaultAsync(item => (item.Code ?? item.Name) == "user", token) ?? new Role { Code = "user", Name = "user" };
            var account = new Account { Username = username, Email = email };
            account.Password = passwords.Hash(account, request.Password!);
            database.Accounts!.Add(account);
            database.AccountRoles!.Add(new AccountRole { Account = account, Role = role });
            challenge.AttemptsLeft = 0; challenge.CodeHash = ""; challenge.ExpiresAt = now;
            await database.SaveChangesAsync(token); await transaction.CommitAsync(token);
            return RegistrationStatus.Success;
        }
        finally
        {
            if (MySql)
            {
                if (lockName is not null)
                {
                    try
                    {
                        await using var release = new MySqlCommand("SELECT RELEASE_LOCK(@name)", (MySqlConnection)database.Database.GetDbConnection()) { CommandTimeout = 5 };
                        release.Parameters.AddWithValue("@name", lockName); await release.ExecuteScalarAsync(CancellationToken.None);
                    }
                    catch (Exception error) { logger.LogWarning("Registration lock cleanup failed. ErrorType: {ErrorType}", error.GetType().Name); }
                }
                await database.Database.CloseConnectionAsync();
            }
        }
    }
}
