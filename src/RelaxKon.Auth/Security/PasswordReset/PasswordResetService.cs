using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace RelaxKon.Auth.Security.PasswordReset;

public interface IPasswordResetService
{
    Task RequestAsync(string username, string email, CancellationToken cancellationToken);
    Task<bool> ResetAsync(string token, string password, CancellationToken cancellationToken);
}

public sealed class PasswordResetService(IPasswordResetStore store, ResetTokenService tokens,
    IPasswordResetEmailSender sender, IDistributedCache cache, IOptions<PasswordResetOptions> options,
    ILogger<PasswordResetService> logger) : IPasswordResetService
{
    public async Task RequestAsync(string username, string email, CancellationToken cancellationToken)
    {
        // The same cooldown and response apply to unknown accounts.
        var key = "auth:reset-cooldown:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            username.Trim().ToLowerInvariant() + "\n" + email.Trim().ToLowerInvariant())));
        if (await cache.GetStringAsync(key, cancellationToken) is not null) return;
        await cache.SetStringAsync(key, "1", new DistributedCacheEntryOptions
            { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) }, cancellationToken);
        var account = await store.FindAsync(username.Trim(), email.Trim(), cancellationToken);
        if (account is null) return;
        var link = options.Value.PublicBaseUrl.TrimEnd('/') + "/accounts/forgot-me-password#token=" + Uri.EscapeDataString(tokens.Create(account));
        try { await sender.SendAsync(account.Email!, link, cancellationToken); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Never log mail recipients, password-reset tokens, links or SMTP credentials.
            logger.LogWarning("Password reset mail delivery failed. ErrorType: {ErrorType}", error.GetType().Name);
        }
    }

    public Task<bool> ResetAsync(string token, string password, CancellationToken cancellationToken)
    {
        if (password.Length is < 8 or > 20 || string.IsNullOrWhiteSpace(password)) return Task.FromResult(false);
        var identity = tokens.Read(token);
        return identity is null ? Task.FromResult(false) : store.ReplacePasswordAsync(identity, password, cancellationToken);
    }
}
