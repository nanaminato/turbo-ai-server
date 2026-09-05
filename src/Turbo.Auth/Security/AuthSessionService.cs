using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Turbo.Auth.Data.Contexts;
using Turbo.Auth.Models.Accounts;
using Turbo.Auth.Models.Config;

namespace Turbo.Auth.Security;

public sealed class AuthSessionService : IAuthSessionService
{
    private const string SecurityStampCachePrefix = "auth:security-stamp:";
    private const string SessionCachePrefix = "auth:session:";
    private readonly AuthContext _context;
    private readonly IDistributedCache _cache;
    private readonly JwtSettings _settings;

    public AuthSessionService(AuthContext context, IDistributedCache cache, IOptions<JwtSettings> settings)
    {
        _context = context;
        _cache = cache;
        _settings = settings.Value;
    }

    public async Task<RefreshTokenIssue> CreateSessionAsync(Account account, string? deviceName, string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        EnsureSecurityStamp(account);
        var issue = CreateIssue(account.AccountId, Guid.NewGuid(), deviceName, ipAddress);
        _context.RefreshTokens!.Add(issue.Entity);
        await _context.SaveChangesAsync(cancellationToken);
        await SetSessionStateAsync(issue.Entity.SessionId, true, cancellationToken);
        return issue.ToResult();
    }

    public async Task<RefreshTokenRotation> RotateAsync(string rawToken, string? deviceName, string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return new RefreshTokenRotation(null, null, false);
        }

        var tokenHash = Hash(rawToken);
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,
            cancellationToken);
        var existing = await _context.RefreshTokens!
            .Include(token => token.Account)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (existing?.Account is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new RefreshTokenRotation(null, null, false);
        }

        var now = DateTime.UtcNow;
        if (existing.RevokedAt is not null || existing.ExpiresAt <= now)
        {
            var replay = existing.RevokedAt is not null && existing.ReplacedByTokenId is not null;
            if (replay)
            {
                await InvalidateAllCoreAsync(existing.Account, now, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new RefreshTokenRotation(null, null, replay);
        }

        existing.RevokedAt = now;
        existing.LastUsedAt = now;
        var next = CreateIssue(existing.AccountId, existing.SessionId,
            string.IsNullOrWhiteSpace(deviceName) ? existing.DeviceName : deviceName,
            string.IsNullOrWhiteSpace(ipAddress) ? existing.CreatedByIp : ipAddress);
        existing.ReplacedByTokenId = next.Entity.RefreshTokenId;
        _context.RefreshTokens!.Add(next.Entity);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await SetSessionStateAsync(existing.SessionId, true, cancellationToken);
        return new RefreshTokenRotation(existing.Account, next.ToResult(), false);
    }

    public async Task RevokeByRefreshTokenAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return;

        var token = await _context.RefreshTokens!
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == Hash(rawToken), cancellationToken);
        if (token is null) return;

        await RevokeSessionAsync(token.AccountId, token.SessionId, cancellationToken);
    }

    public async Task<bool> RevokeSessionAsync(int accountId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var revoked = await _context.RefreshTokens!
            .Where(token => token.AccountId == accountId && token.SessionId == sessionId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
        await SetSessionStateAsync(sessionId, false, cancellationToken);
        return revoked > 0;
    }

    public async Task InvalidateAllAsync(int accountId, CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts!
            .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId, cancellationToken);
        if (account is null) return;

        await InvalidateAllCoreAsync(account, DateTime.UtcNow, cancellationToken);
    }

    public async Task<IReadOnlyList<AuthSession>> GetActiveSessionsAsync(int accountId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _context.RefreshTokens!
            .AsNoTracking()
            .Where(token => token.AccountId == accountId && token.RevokedAt == null && token.ExpiresAt > now)
            .OrderByDescending(token => token.LastUsedAt ?? token.CreatedAt)
            .Select(token => new AuthSession(token.SessionId, token.DeviceName, token.CreatedByIp, token.CreatedAt,
                token.LastUsedAt ?? token.CreatedAt, token.ExpiresAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsAccessTokenValidAsync(int accountId, string securityStamp, Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var currentStamp = await GetSecurityStampAsync(accountId, cancellationToken);
        if (currentStamp is null || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(currentStamp), Encoding.UTF8.GetBytes(securityStamp)))
        {
            return false;
        }

        return await IsSessionActiveAsync(accountId, sessionId, cancellationToken);
    }

    private async Task InvalidateAllCoreAsync(Account account, DateTime now, CancellationToken cancellationToken)
    {
        var activeSessionIds = await _context.RefreshTokens!
            .Where(token => token.AccountId == account.AccountId && token.RevokedAt == null)
            .Select(token => token.SessionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        account.SecurityStamp = NewSecurityStamp();
        await _context.RefreshTokens!
            .Where(token => token.AccountId == account.AccountId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await SetSecurityStampAsync(account.AccountId, account.SecurityStamp, cancellationToken);

        foreach (var sessionId in activeSessionIds)
        {
            await SetSessionStateAsync(sessionId, false, cancellationToken);
        }
    }

    private async Task<string?> GetSecurityStampAsync(int accountId, CancellationToken cancellationToken)
    {
        var cacheKey = SecurityStampCachePrefix + accountId;
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached)) return cached;

        var stamp = await _context.Accounts!
            .AsNoTracking()
            .Where(account => account.AccountId == accountId)
            .Select(account => account.SecurityStamp)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(stamp)) return null;

        await SetSecurityStampAsync(accountId, stamp, cancellationToken);
        return stamp;
    }

    private async Task<bool> IsSessionActiveAsync(int accountId, Guid sessionId, CancellationToken cancellationToken)
    {
        var cacheKey = SessionCachePrefix + sessionId.ToString("N");
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached is not null) return cached == "1";

        var now = DateTime.UtcNow;
        var active = await _context.RefreshTokens!
            .AsNoTracking()
            .AnyAsync(token => token.AccountId == accountId && token.SessionId == sessionId &&
                               token.RevokedAt == null && token.ExpiresAt > now, cancellationToken);
        await SetSessionStateAsync(sessionId, active, cancellationToken);
        return active;
    }

    private async Task SetSecurityStampAsync(int accountId, string securityStamp, CancellationToken cancellationToken) =>
        await _cache.SetStringAsync(SecurityStampCachePrefix + accountId, securityStamp, CacheOptions(), cancellationToken);

    private async Task SetSessionStateAsync(Guid sessionId, bool active, CancellationToken cancellationToken) =>
        await _cache.SetStringAsync(SessionCachePrefix + sessionId.ToString("N"), active ? "1" : "0", CacheOptions(),
            cancellationToken);

    private DistributedCacheEntryOptions CacheOptions() => new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(1, _settings.AuthenticationStateCacheSeconds))
    };

    private PendingIssue CreateIssue(int accountId, Guid sessionId, string? deviceName, string? ipAddress)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var now = DateTime.UtcNow;
        var token = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            AccountId = accountId,
            TokenHash = Hash(rawToken),
            SessionId = sessionId,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_settings.RefreshTokenDays),
            DeviceName = Trim(deviceName, 256),
            CreatedByIp = Trim(ipAddress, 64)
        };
        return new PendingIssue(token, rawToken);
    }

    private string Hash(string rawToken)
    {
        var pepper = _settings.RefreshTokenPepper!;
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(pepper), Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NewSecurityStamp() => Guid.NewGuid().ToString("N");

    private static void EnsureSecurityStamp(Account account)
    {
        if (string.IsNullOrWhiteSpace(account.SecurityStamp)) account.SecurityStamp = NewSecurityStamp();
    }

    private static string? Trim(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, maximumLength)];

    private sealed record PendingIssue(RefreshToken Entity, string RawToken)
    {
        public RefreshTokenIssue ToResult() => new(Entity.SessionId, RawToken, Entity.ExpiresAt);
    }
}
