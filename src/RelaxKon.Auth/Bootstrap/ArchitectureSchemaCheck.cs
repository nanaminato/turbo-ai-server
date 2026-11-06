using MySqlConnector;
using Microsoft.Data.Sqlite;
using RelaxKon.Auth.Data;

namespace RelaxKon.Auth.Bootstrap;

/// <summary>Read-only startup preflight. Normal startup must never run schema DDL.</summary>
public static class ArchitectureSchemaCheck
{
    private static readonly Dictionary<string, string[]> RequiredColumns = new(StringComparer.Ordinal)
    {
        ["Roles"] = ["Code", "Description"],
        ["FeatureAccessTasks"] = ["TaskId", "AccountId", "SupplierKeyId"],
        ["RoutingConfiguration"] = ["Id", "Version"],
        ["SyncAccounts"] = ["AccountId", "Version"],
        ["SyncChanges"] = ["AccountId", "Version", "OperationId", "RequestHash", "Kind", "Action", "EntityId", "HistoryId", "Payload"],
        ["ModelKeyBinds"] = ["ProviderModelValue", "Priority", "RoutingWeight"],
        ["RegistrationEmailChallenges"] = ["EmailHash", "CodeHash", "ExpiresAt", "NextSendAt", "AttemptsLeft"]
    };

    private static readonly Dictionary<string, string[]> SqliteRequiredColumns = new(StringComparer.Ordinal)
    {
        ["__efmigrationshistory"] = ["MigrationId", "ProductVersion"],
        ["Accounts"] = ["AccountId", "Username", "Password", "SecurityStamp", "Email"],
        ["RefreshTokens"] = ["RefreshTokenId", "AccountId", "TokenHash", "SessionId", "CreatedAt", "ExpiresAt", "RevokedAt", "LastUsedAt", "ReplacedByTokenId", "DeviceName", "CreatedByIp"],
        ["Roles"] = ["RoleId", "Code", "Name", "Description"],
        ["AccountRoles"] = ["AccountRoleId", "AccountId", "RoleId"],
        ["Models"] = ["ModelId", "Name", "IsChatModel", "ModelValue", "Vision"],
        ["SupplierKeys"] = ["SupplierKeyId", "BaseUrl", "RequestIdentifier", "Enable", "ApiKey"],
        ["ModelFees"] = ["ModelFeeId", "SupplierKeyId", "ModelId", "Fee"],
        ["ChatHistories"] = ["ChatHistoryId", "UserId", "DataId", "Title"],
        ["ChatMessages"] = ["ChatMessageId", "DataId", "ChatHistoryId", "Role", "Content", "ShowType", "Finish", "Model"],
        ["FileAdds"] = ["FileAddsId", "FileName", "FileType", "FileSize", "FileContent", "ParsedContent", "ChatMessageId"],
        ["GenerateTasks"] = ["TaskId", "TaskType", "AccountId", "DateTime", "Images", "Videos"],
        ["ModelKeyBinds"] = ["ModelKeyBindId", "Enable", "SupplierKeyId", "ModelId", "Fee", "RoutingWeight", "ProviderModelValue", "Priority"],
        ["AvailableModels"] = ["ModelId", "Enable", "Name", "IsChatModel", "ModelValue", "Vision"],
        ["FeatureAccessPolicies"] = ["Id", "Revision", "Document"],
        ["FeatureAccessUsage"] = ["AccountId", "FeatureKey", "MinuteWindow", "DayWindow", "MinuteCount", "DayCount"],
        ["FeatureAccessLeases"] = ["LeaseId", "AccountId", "FeatureKey", "ExpiresAt"],
        ["FeatureAccessTasks"] = ["TaskId", "AccountId", "SupplierKeyId", "CreatedAt"],
        ["SyncAccounts"] = ["AccountId", "Version"],
        ["SyncChanges"] = ["AccountId", "Version", "OperationId", "RequestHash", "Kind", "Action", "EntityId", "HistoryId", "Payload"],
        ["RegistrationEmailChallenges"] = ["EmailHash", "CodeHash", "ExpiresAt", "NextSendAt", "AttemptsLeft"],
        ["RoutingConfiguration"] = ["Id", "Version"],
    };

    public static Task Validate(DatabaseProvider provider, CancellationToken token = default, bool includeRegistration = false) =>
        provider.IsSqlite ? ValidateSqlite(provider, token) : Validate(provider.ConnectionString, token, includeRegistration);

    private static async Task ValidateSqlite(DatabaseProvider provider, CancellationToken token)
    {
        // Never create a missing database as a side effect of starting the server.
        var settings = new SqliteConnectionStringBuilder(provider.ConnectionString)
        {
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        await using var connection = new SqliteConnection(settings.ConnectionString);
        try { await connection.OpenAsync(token); }
        catch (SqliteException error)
        {
            throw new InvalidOperationException("无法打开 SQLite 数据库。请检查文件路径和权限；新数据库须先显式运行 --init-db。启动不会创建数据库。", error);
        }
        async Task<List<string[]>> Read(string sql)
        {
            await using var command = new SqliteCommand(sql, connection) { CommandTimeout = 5 };
            await using var reader = await command.ExecuteReaderAsync(token);
            var rows = new List<string[]>();
            while (await reader.ReadAsync(token))
                rows.Add(Enumerable.Range(0, reader.FieldCount).Select(i => Convert.ToString(reader.GetValue(i))!).ToArray());
            return rows;
        }
        var journal = await Read("PRAGMA journal_mode");
        if (journal.Count != 1 || !string.Equals(journal[0][0], "wal", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SQLite 数据库必须启用 WAL。请停止服务并备份，在维护连接中执行 PRAGMA journal_mode=WAL 并确认返回 wal；不要重跑 --init-db。启动不会修改日志模式。");
        var missing = new List<string>();
        foreach (var (table, required) in SqliteRequiredColumns)
        {
            var available = (await Read($"SELECT name FROM pragma_table_info('{table}')"))
                .Select(row => row[0]).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (available.Count == 0) missing.Add(table);
            else missing.AddRange(required.Where(column => !available.Contains(column)).Select(column => table + "." + column));
        }
        foreach (var (table, fields) in new[] { ("ChatHistories", "UserId,DataId"), ("ChatMessages", "ChatHistoryId,DataId"),
            ("SyncChanges", "AccountId,Version"), ("SyncChanges", "AccountId,OperationId"), ("Roles", "Code"), ("AccountRoles", "AccountId,RoleId"),
            ("FeatureAccessPolicies", "Id"), ("FeatureAccessUsage", "AccountId,FeatureKey"),
            ("FeatureAccessLeases", "LeaseId"), ("FeatureAccessTasks", "TaskId"), ("RefreshTokens", "TokenHash"),
            ("RoutingConfiguration", "Id"), ("SyncAccounts", "AccountId") })
        {
            // INTEGER PRIMARY KEY aliases the rowid and has no pragma_index_list entry.
            var primaryKey = await Read($"SELECT name FROM pragma_table_info('{table}') WHERE pk>0 ORDER BY pk");
            var found = string.Join(",", primaryKey.Select(row => row[0])) == fields;
            // A partial unique index cannot enforce the invariant for every store row.
            var indexes = await Read($"SELECT name FROM pragma_index_list('{table}') WHERE [unique]=1 AND partial=0");
            foreach (var index in indexes)
            {
                var escaped = index[0].Replace("'", "''");
                var columns = await Read($"SELECT name FROM pragma_index_info('{escaped}') ORDER BY seqno");
                found |= string.Join(",", columns.Select(row => row[0])) == fields;
            }
            if (!found) missing.Add(table + " unique(" + fields + ")");
        }
        var triggers = await Read("SELECT name FROM sqlite_schema WHERE type='trigger'");
        foreach (var table in new[] { "Models", "SupplierKeys", "ModelKeyBinds", "AvailableModels" })
            foreach (var action in new[] { "insert", "update", "delete" })
            {
                var name = $"routing_{table}_{action}";
                if (!triggers.Any(row => row[0] == name)) missing.Add(name);
            }
        if (missing.Count == 0)
        {
            var seed = await Read("SELECT COUNT(*) FROM RoutingConfiguration WHERE Id=1");
            if (seed[0][0] != "1") missing.Add("RoutingConfiguration row Id=1");
        }
        if (missing.Count != 0)
            throw new InvalidOperationException("SQLite 数据库架构不完整，缺少：" + string.Join(", ", missing) +
                "。请停止写入并备份后核对架构；不要在已有数据库上重跑初始化脚本。新空库请显式运行 --init-db。启动不会自动修改数据库。");
    }

    public static async Task Validate(string connectionString, CancellationToken token = default, bool includeRegistration = false)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(token);
        async Task<List<string[]>> Read(string sql)
        {
            await using var command = new MySqlCommand(sql, connection) { CommandTimeout = 5 };
            await using var reader = await command.ExecuteReaderAsync(token);
            var rows = new List<string[]>();
            while (await reader.ReadAsync(token))
                rows.Add(Enumerable.Range(0, reader.FieldCount).Select(reader.GetString).ToArray());
            return rows;
        }
        var columns = await Read("SELECT TABLE_NAME,COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()");
        var missing = new List<string>();
        foreach (var (table, required) in RequiredColumns)
        {
            if (table == "RegistrationEmailChallenges" && !includeRegistration) continue;
            var available = columns.Where(row => row[0] == table).Select(row => row[1]).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (available.Count == 0) missing.Add(table);
            else missing.AddRange(required.Where(column => !available.Contains(column)).Select(column => table + "." + column));
        }
        var indexes = await Read("SELECT TABLE_NAME,GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND NON_UNIQUE=0 GROUP BY TABLE_NAME,INDEX_NAME");
        foreach (var (table, fields) in new[] { ("ChatHistories", "UserId,DataId"), ("ChatMessages", "ChatHistoryId,DataId"),
            ("SyncChanges", "AccountId,Version"), ("SyncChanges", "AccountId,OperationId"), ("Roles", "Code"), ("AccountRoles", "AccountId,RoleId") })
            if (!indexes.Any(row => row[0] == table && row[1] == fields)) missing.Add(table + " unique(" + fields + ")");
        var triggers = await Read("SELECT TRIGGER_NAME FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE()");
        foreach (var table in new[] { "Models", "SupplierKeys", "ModelKeyBinds", "AvailableModels" })
            foreach (var action in new[] { "insert", "update", "delete" })
            {
                var name = $"routing_{table}_{action}";
                if (!triggers.Any(row => row[0] == name)) missing.Add(name);
            }
        if (missing.Count == 0)
        {
            await using var seed = new MySqlCommand("SELECT COUNT(*) FROM RoutingConfiguration WHERE Id=1", connection) { CommandTimeout = 5 };
            if (Convert.ToInt64(await seed.ExecuteScalarAsync(token)) != 1) missing.Add("RoutingConfiguration row Id=1");
        }
        if (missing.Count != 0)
        {
            var scripts = new List<string>();
            if (missing.Contains("FeatureAccessTasks"))
                scripts.Add("src/RelaxKon.Auth/Resources/merge/feature-access.sql");
            else if (missing.Any(item => item.StartsWith("FeatureAccessTasks.", StringComparison.Ordinal)))
                scripts.Add("src/RelaxKon.Auth/Resources/merge/apimart-task-route-upgrade.sql");
            if (missing.Any(item => item.StartsWith("Roles.") || item.StartsWith("Roles unique") || item.StartsWith("AccountRoles unique")))
                scripts.Add("src/RelaxKon.Auth/Resources/merge/role-system-upgrade.sql");
            if (missing.Contains("ModelKeyBinds.RoutingWeight"))
                scripts.Add("src/RelaxKon.Auth/Resources/merge/routing-weight-upgrade.sql");
            if (missing.Any(item => item != "ModelKeyBinds.RoutingWeight" && !item.StartsWith("Roles") && !item.StartsWith("AccountRoles") && !item.StartsWith("FeatureAccessTasks", StringComparison.Ordinal) && !item.StartsWith("RegistrationEmailChallenges", StringComparison.Ordinal)))
                scripts.Add("src/RelaxKon.Auth/Resources/merge/architecture-upgrade.sql");
            if (missing.Any(item => item.StartsWith("RegistrationEmailChallenges", StringComparison.Ordinal)))
                scripts.Add("src/RelaxKon.Auth/Resources/merge/registration-email-verification.sql");
            throw new InvalidOperationException("数据库架构尚未升级，缺少：" + string.Join(", ", missing) +
                "。请停止写入并备份；已有库对照 " + string.Join("、", scripts) + " 完成迁移。" +
                "部分升级的数据库需先核对实际结构，不能盲目整份重跑。空库请使用 merge/init.sql。详见 docs/deployment.md。启动不会自动修改数据库。");
        }
    }
}

public sealed class DatabaseSchemaValidationService(IConfiguration configuration,
    Microsoft.Extensions.Options.IOptions<RelaxKon.Auth.Security.Registration.RegistrationOptions> registration,
    Microsoft.Extensions.Options.IOptions<RelaxKon.Auth.Security.PasswordReset.PasswordResetOptions> smtp) : IHostedService
{
    public async Task StartAsync(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await ArchitectureSchemaCheck.Validate(DatabaseProvider.FromConfiguration(configuration), timeout.Token, registration.Value.Enabled && smtp.Value.IsSmtpValid());
    }
    public Task StopAsync(CancellationToken token) => Task.CompletedTask;
}
