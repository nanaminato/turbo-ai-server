using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using RelaxKon.Auth.Data.Contexts;
using RelaxKon.Auth.Models.Accounts;

namespace RelaxKon.Auth.Security.PasswordReset;

public interface IPasswordResetStore
{
    Task<Account?> FindAsync(string username, string email, CancellationToken cancellationToken);
    Task<bool> ReplacePasswordAsync(ResetIdentity identity, string password, CancellationToken cancellationToken);
}

public sealed class PasswordResetStore(AuthContext context, IAccountPasswordService passwords, IDistributedCache cache) : IPasswordResetStore
{
    public async Task<Account?> FindAsync(string username, string email, CancellationToken cancellationToken)
    {
        // Legacy databases may contain duplicate names/emails. Never pick an arbitrary account.
        var accounts = await context.Accounts!.AsNoTracking().Where(a => a.Username == username && a.Email == email)
            .Take(2).ToListAsync(cancellationToken);
        return accounts.Count == 1 ? accounts[0] : null;
    }

    public async Task<bool> ReplacePasswordAsync(ResetIdentity identity, string password, CancellationToken cancellationToken)
    {
        var account = await context.Accounts!.AsNoTracking().SingleOrDefaultAsync(a => a.AccountId == identity.AccountId, cancellationToken);
        if (account is null || !ResetTokenService.Matches(identity, account)) return false;
        var previousPassword = account.Password;
        var stamp = Guid.NewGuid().ToString("N");
        var hash = passwords.Hash(account, password);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        // Optimistic update makes the token single-use even for simultaneous submissions.
        var updated = await context.Accounts!.Where(a => a.AccountId == identity.AccountId && a.Email == identity.Email &&
            a.SecurityStamp == identity.SecurityStamp && a.Password == previousPassword)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Password, hash).SetProperty(a => a.SecurityStamp, stamp), cancellationToken);
        if (updated != 1) return false;
        var sessions = await context.RefreshTokens!.Where(t => t.AccountId == identity.AccountId && t.RevokedAt == null)
            .Select(t => t.SessionId).Distinct().ToListAsync(cancellationToken);
        await context.RefreshTokens!.Where(t => t.AccountId == identity.AccountId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        // Match AuthSessionService cache keys. Remove after commit so old JWT/session caches are refreshed.
        await cache.RemoveAsync("auth:security-stamp:" + identity.AccountId, CancellationToken.None);
        foreach (var session in sessions) await cache.RemoveAsync("auth:session:" + session.ToString("N"), CancellationToken.None);
        return true;
    }
}
