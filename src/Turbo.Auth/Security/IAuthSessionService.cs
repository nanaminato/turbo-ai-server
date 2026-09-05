using Turbo.Auth.Models.Accounts;

namespace Turbo.Auth.Security;

public interface IAuthSessionService
{
    Task<RefreshTokenIssue> CreateSessionAsync(Account account, string? deviceName, string? ipAddress,
        CancellationToken cancellationToken = default);
    Task<RefreshTokenRotation> RotateAsync(string rawToken, string? deviceName, string? ipAddress,
        CancellationToken cancellationToken = default);
    Task RevokeByRefreshTokenAsync(string rawToken, CancellationToken cancellationToken = default);
    Task<bool> RevokeSessionAsync(int accountId, Guid sessionId, CancellationToken cancellationToken = default);
    Task InvalidateAllAsync(int accountId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuthSession>> GetActiveSessionsAsync(int accountId, CancellationToken cancellationToken = default);
    Task<bool> IsAccessTokenValidAsync(int accountId, string securityStamp, Guid sessionId,
        CancellationToken cancellationToken = default);
}

public sealed record RefreshTokenIssue(Guid SessionId, string RefreshToken, DateTime RefreshTokenExpiresAt);

public sealed record RefreshTokenRotation(Account? Account, RefreshTokenIssue? Issue, bool IsReplay)
{
    public bool Succeeded => Account is not null && Issue is not null;
}

public sealed record AuthSession(Guid SessionId, string? DeviceName, string? IpAddress, DateTime CreatedAt,
    DateTime LastUsedAt, DateTime ExpiresAt);
