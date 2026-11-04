using System.Text.Json;
using MySqlConnector;

namespace Turbo.Auth.Security.FeatureAccess;

// All replicas share the same MySQL rows. No process-local fallback is allowed.
public sealed class FeatureAccessStore(IConfiguration configuration)
{
    private async Task<MySqlConnection> Open(CancellationToken token)
    {
        var connection = new MySqlConnection(configuration.GetConnectionString("ciko"));
        await connection.OpenAsync(token);
        return connection;
    }
    private static MySqlCommand Command(MySqlConnection connection, MySqlTransaction? transaction, string sql,
        params (string, object?)[] parameters)
    {
        var command = new MySqlCommand(sql, connection, transaction);
        foreach (var (key, value) in parameters) command.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return command;
    }
    public async Task<FeaturePolicyDocument> Read(CancellationToken token)
    {
        await using var connection = await Open(token);
        await using var command = Command(connection, null, "SELECT Revision, Document FROM FeatureAccessPolicies WHERE Id=1");
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return FeaturePolicies.Empty;
        return JsonSerializer.Deserialize<FeaturePolicyDocument>(reader.GetString(1))! with { Revision = reader.GetInt64(0) };
    }
    public async Task<bool> Save(FeaturePolicyDocument document, CancellationToken token)
    {
        await using var connection = await Open(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await using (var seed = Command(connection, transaction,
            "INSERT INTO FeatureAccessPolicies (Id,Revision,Document) VALUES (1,0,@document) ON DUPLICATE KEY UPDATE Id=1",
            ("@document", JsonSerializer.Serialize(FeaturePolicies.Empty)))) await seed.ExecuteNonQueryAsync(token);
        await using var update = Command(connection, transaction,
            "UPDATE FeatureAccessPolicies SET Revision=Revision+1,Document=@document WHERE Id=1 AND Revision=@revision",
            ("@document", JsonSerializer.Serialize(document)), ("@revision", document.Revision));
        var changed = await update.ExecuteNonQueryAsync(token) == 1;
        await transaction.CommitAsync(token);
        return changed;
    }
    public async Task<Admission> Acquire(int accountId, EffectiveFeature feature, CancellationToken token)
    {
        await using var connection = await Open(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var args = new (string, object?)[] { ("@account", accountId), ("@feature", feature.Key) };
        await using (var seed = Command(connection, transaction,
            "INSERT INTO FeatureAccessUsage (AccountId,FeatureKey) VALUES (@account,@feature) ON DUPLICATE KEY UPDATE AccountId=@account", args))
            await seed.ExecuteNonQueryAsync(token);
        // A stable account+feature row serializes all admission decisions, even at window boundaries.
        long minute, day, minuteCount, dayCount, now;
        await using (var read = Command(connection, transaction,
            "SELECT MinuteWindow,DayWindow,MinuteCount,DayCount,UNIX_TIMESTAMP() FROM FeatureAccessUsage WHERE AccountId=@account AND FeatureKey=@feature FOR UPDATE", args))
        await using (var reader = await read.ExecuteReaderAsync(token))
        {
            await reader.ReadAsync(token);
            minute=reader.GetInt64(0); day=reader.GetInt64(1); minuteCount=reader.GetInt64(2); dayCount=reader.GetInt64(3); now=reader.GetInt64(4);
        }
        if (minute != now / 60) minuteCount=0;
        if (day != now / 86400) dayCount=0;
        await using (var cleanup = Command(connection, transaction,
            "DELETE FROM FeatureAccessLeases WHERE AccountId=@account AND FeatureKey=@feature AND ExpiresAt<=UTC_TIMESTAMP()", args))
            await cleanup.ExecuteNonQueryAsync(token);
        await using var active = Command(connection, transaction,
            "SELECT COUNT(*) FROM FeatureAccessLeases WHERE AccountId=@account AND FeatureKey=@feature", args);
        var concurrent = Convert.ToInt64(await active.ExecuteScalarAsync(token));
        var retry = feature.RequestsPerDay is { } daily && dayCount >= daily ? 86400 - now % 86400
            : feature.RequestsPerMinute is { } rate && minuteCount >= rate ? 60 - now % 60
            : feature.MaxConcurrent is { } max && concurrent >= max ? 1 : 0;
        if (retry > 0) return new(null, (int)retry);
        var id = Guid.NewGuid().ToString("N");
        await using (var update = Command(connection, transaction,
            "UPDATE FeatureAccessUsage SET MinuteWindow=@minute,DayWindow=@day,MinuteCount=@mc,DayCount=@dc WHERE AccountId=@account AND FeatureKey=@feature",
            args.Concat(new (string, object?)[] { ("@minute", now/60), ("@day", now/86400), ("@mc", minuteCount+1), ("@dc", dayCount+1) }).ToArray()))
            await update.ExecuteNonQueryAsync(token);
        await using (var lease = Command(connection, transaction,
            "INSERT INTO FeatureAccessLeases (LeaseId,AccountId,FeatureKey,ExpiresAt) VALUES (@id,@account,@feature,DATE_ADD(UTC_TIMESTAMP(),INTERVAL 90 SECOND))",
            args.Append(("@id", (object?)id)).ToArray())) await lease.ExecuteNonQueryAsync(token);
        await transaction.CommitAsync(token);
        return new(id, 0);
    }
    public async Task RecordTask(int accountId, string taskId, CancellationToken token)
    {
        await using var connection = await Open(token);
        await using var command = Command(connection, null,
            "INSERT INTO FeatureAccessTasks (TaskId,AccountId) VALUES (@id,@account)", ("@id", taskId), ("@account",accountId));
        await command.ExecuteNonQueryAsync(token);
    }
    public async Task<bool> OwnsTask(int accountId, string taskId, CancellationToken token)
    {
        await using var connection = await Open(token);
        await using var command = Command(connection, null,
            "SELECT COUNT(*) FROM FeatureAccessTasks WHERE TaskId=@id AND AccountId=@account", ("@id",taskId), ("@account",accountId));
        return Convert.ToInt64(await command.ExecuteScalarAsync(token)) == 1;
    }
    public async Task<bool> Renew(string id, CancellationToken token)
    {
        await using var connection = await Open(token);
        await using var command = Command(connection, null,
            "UPDATE FeatureAccessLeases SET ExpiresAt=DATE_ADD(UTC_TIMESTAMP(),INTERVAL 90 SECOND) WHERE LeaseId=@id AND ExpiresAt>UTC_TIMESTAMP()", ("@id",id));
        return await command.ExecuteNonQueryAsync(token) == 1;
    }
    public async Task Release(string id, CancellationToken token)
    {
        await using var connection = await Open(token);
        await using var command = Command(connection, null,"DELETE FROM FeatureAccessLeases WHERE LeaseId=@id", ("@id",id));
        await command.ExecuteNonQueryAsync(token);
    }
}
public sealed record Admission(string? LeaseId, int RetryAfterSeconds);
