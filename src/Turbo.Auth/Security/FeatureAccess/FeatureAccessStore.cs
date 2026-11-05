using System.Text.Json;
using Turbo.Auth.Application.Media;
using MySqlConnector;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Turbo.Auth.Data.Contexts;

namespace Turbo.Auth.Security.FeatureAccess;

// All replicas share the same MySQL rows. No process-local fallback is allowed.
public sealed class FeatureAccessStore(IConfiguration configuration) : IImageTaskReceipts
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
        // Match role-management lock order: roles first, then the policy document.
        await using (var roleLock = Command(connection, transaction,
            "SELECT RoleId FROM Roles WHERE Code='admin' FOR UPDATE")) await roleLock.ExecuteScalarAsync(token);
        var codes = new HashSet<string>(StringComparer.Ordinal);
        await using (var roles = Command(connection, transaction, "SELECT Code FROM Roles"))
        await using (var reader = await roles.ExecuteReaderAsync(token))
            while (await reader.ReadAsync(token)) codes.Add(reader.GetString(0));
        if (document.Revision < 0 || !FeaturePolicies.IsValid(document, codes))
            throw new RoleRuleException("invalid_feature_policy");
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
    // Called with the role-management lock held, inside the caller's transaction.
    public static async Task RemoveRole(AuthContext database, string code)
    {
        var connection = database.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = database.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("Role-policy cleanup requires a transaction.");
        command.CommandText = database.Database.IsMySql()
            ? "INSERT INTO FeatureAccessPolicies (Id,Revision,Document) VALUES (1,0,@empty) ON DUPLICATE KEY UPDATE Id=1"
            : "INSERT INTO FeatureAccessPolicies (Id,Revision,Document) VALUES (1,0,@empty) ON CONFLICT(Id) DO NOTHING";
        var empty = command.CreateParameter(); empty.ParameterName = "@empty";
        empty.Value = JsonSerializer.Serialize(FeaturePolicies.Empty); command.Parameters.Add(empty);
        await command.ExecuteNonQueryAsync(); command.Parameters.Clear();
        command.CommandText = "SELECT Revision, Document FROM FeatureAccessPolicies WHERE Id=1" +
            (database.Database.IsMySql() ? " FOR UPDATE" : "");
        FeaturePolicyDocument document;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync()) return;
            document = JsonSerializer.Deserialize<FeaturePolicyDocument>(reader.GetString(1))! with { Revision = reader.GetInt64(0) + 1 };
        }
        foreach (var feature in document.Features) feature.Roles.Remove(code);
        command.CommandText = "UPDATE FeatureAccessPolicies SET Revision=@revision,Document=@document WHERE Id=1";
        foreach (var (name, value) in new (string, object)[] { ("@revision", document.Revision), ("@document", JsonSerializer.Serialize(document)) })
        {
            var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        await command.ExecuteNonQueryAsync();
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
    public async Task RecordTask(int accountId, string taskId, CancellationToken token, int? supplierKeyId = null)
    {
        await using var connection = await Open(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await using (var command = Command(connection, transaction,
            "INSERT INTO FeatureAccessTasks (TaskId,AccountId,SupplierKeyId) VALUES (@id,@account,@key) ON DUPLICATE KEY UPDATE TaskId=TaskId",
            ("@id", taskId), ("@account",accountId), ("@key",supplierKeyId)))
            await command.ExecuteNonQueryAsync(token);
        await using (var check = Command(connection, transaction,
            "SELECT AccountId,SupplierKeyId FROM FeatureAccessTasks WHERE TaskId=@id FOR UPDATE", ("@id",taskId)))
        await using (var reader = await check.ExecuteReaderAsync(token))
        {
            if (!await reader.ReadAsync(token) || reader.GetInt32(0) != accountId ||
                (reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1)) != supplierKeyId)
                throw new InvalidOperationException("The task receipt belongs to a different owner or route.");
        }
        await transaction.CommitAsync(token);
    }
    public async Task<int?> GetTaskSupplierKeyId(int accountId, string taskId, CancellationToken token)
    {
        await using var connection = await Open(token);
        await using var command = Command(connection, null,
            "SELECT SupplierKeyId FROM FeatureAccessTasks WHERE TaskId=@id AND AccountId=@account",
            ("@id",taskId), ("@account",accountId));
        var value = await command.ExecuteScalarAsync(token);
        return value is null or DBNull ? null : Convert.ToInt32(value);
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
