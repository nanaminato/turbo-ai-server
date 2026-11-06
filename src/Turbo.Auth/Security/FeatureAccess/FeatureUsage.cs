using System.Text.Json;
using Turbo.Auth.Data;

namespace Turbo.Auth.Security.FeatureAccess;

public sealed record FeatureUsageCounter(string Key, long MinuteWindow, long DayWindow,
    long MinuteCount, long DayCount, long ActiveRequests);
public sealed record FeatureUsage(EffectiveFeature Policy, long MinuteUsed, long DayUsed,
    long ActiveRequests, long? MinuteRemaining, long? DayRemaining, long? ConcurrentRemaining);
public sealed record FeatureUsageSnapshot(DateTimeOffset ObservedAt, long PolicyRevision,
    DateTimeOffset MinuteResetsAt, DateTimeOffset DayResetsAt, IReadOnlyList<FeatureUsage> Features);

public interface IFeatureUsageReader
{
    Task<FeatureUsageSnapshot> ReadUsage(int accountId, CancellationToken token);
}

public static class FeatureUsageProjection
{
    public static FeatureUsageSnapshot Create(long now, FeaturePolicyDocument document,
        IEnumerable<string> roles, IEnumerable<FeatureUsageCounter> counters)
    {
        var byFeature = counters.ToDictionary(counter => counter.Key, StringComparer.Ordinal);
        var roleCodes = roles.ToArray();
        var features = FeaturePolicies.Keys.Select(key =>
        {
            var policy = FeaturePolicies.Resolve(document, key, roleCodes);
            byFeature.TryGetValue(key, out var counter);
            var minute = counter?.MinuteWindow == now / 60 ? Math.Max(0, counter.MinuteCount) : 0;
            var day = counter?.DayWindow == now / 86400 ? Math.Max(0, counter.DayCount) : 0;
            var active = Math.Max(0, counter?.ActiveRequests ?? 0);
            long? Remaining(int? limit, long used) => !policy.Enabled ? 0
                : limit is { } value ? Math.Max(0L, value - used) : null;
            return new FeatureUsage(policy, minute, day, active, Remaining(policy.RequestsPerMinute, minute),
                Remaining(policy.RequestsPerDay, day), Remaining(policy.MaxConcurrent, active));
        }).ToList();
        return new(DateTimeOffset.FromUnixTimeSeconds(now), document.Revision,
            DateTimeOffset.FromUnixTimeSeconds((now / 60 + 1) * 60),
            DateTimeOffset.FromUnixTimeSeconds((now / 86400 + 1) * 86400), features);
    }
}

// Advisory, read-only snapshot. Admission remains authoritative and may change immediately after this read.
public sealed class FeatureUsageReader(IConfiguration configuration) : IFeatureUsageReader
{
    private readonly DatabaseProvider provider = DatabaseProvider.FromConfiguration(configuration);

    public async Task<FeatureUsageSnapshot> ReadUsage(int accountId, CancellationToken token)
    {
        if (accountId <= 0) throw new ArgumentOutOfRangeException(nameof(accountId));
        await using var connection = provider.CreateConnection();
        await connection.OpenAsync(token);
        // Policy, current roles, counters and leases share one consistent database snapshot.
        // No FOR UPDATE, cleanup, seeding or writes: viewing usage never consumes or resets quota.
        await using var transaction = await provider.BeginRead(connection, token);
        long now;
        object utc;
        FeaturePolicyDocument document;
        await using (var command = provider.Command(connection, transaction, provider.Choose("""
            SELECT UNIX_TIMESTAMP(), UTC_TIMESTAMP(), policy.Revision, policy.Document
            FROM (SELECT 1 AS Id) singleton
            LEFT JOIN FeatureAccessPolicies policy ON policy.Id=singleton.Id
            """, """
            SELECT CAST(strftime('%s','now') AS INTEGER), datetime('now'), policy.Revision, policy.Document
            FROM (SELECT 1 AS Id) singleton
            LEFT JOIN FeatureAccessPolicies policy ON policy.Id=singleton.Id
            """)))
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            await reader.ReadAsync(token);
            now = reader.GetInt64(0);
            utc = provider.IsSqlite ? reader.GetString(1) : reader.GetDateTime(1);
            document = reader.IsDBNull(2) ? FeaturePolicies.Empty
                : JsonSerializer.Deserialize<FeaturePolicyDocument>(reader.GetString(3))!
                    with { Revision = reader.GetInt64(2) };
        }
        var roles = new List<string>();
        await using (var command = provider.Command(connection, transaction, """
            SELECT DISTINCT COALESCE(r.Code,r.Name) FROM AccountRoles ar
            INNER JOIN Roles r ON r.RoleId=ar.RoleId
            WHERE ar.AccountId=@account AND COALESCE(r.Code,r.Name) IS NOT NULL
            """, ("@account", accountId)))
        {
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) roles.Add(reader.GetString(0));
        }
        var counters = new List<FeatureUsageCounter>();
        await using (var command = provider.Command(connection, transaction, """
            SELECT u.FeatureKey,u.MinuteWindow,u.DayWindow,u.MinuteCount,u.DayCount,
                (SELECT COUNT(*) FROM FeatureAccessLeases l
                 WHERE l.AccountId=u.AccountId AND l.FeatureKey=u.FeatureKey
                    AND l.ExpiresAt>@utc)
            FROM FeatureAccessUsage u WHERE u.AccountId=@account
            """, ("@account", accountId), ("@utc", utc)))
        {
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) counters.Add(new(reader.GetString(0), reader.GetInt64(1),
                reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(5)));
        }
        await transaction.CommitAsync(token);
        return FeatureUsageProjection.Create(now, document, roles, counters);
    }
}
