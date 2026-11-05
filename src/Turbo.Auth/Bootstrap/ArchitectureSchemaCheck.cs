using MySqlConnector;

namespace Turbo.Auth.Bootstrap;

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
                scripts.Add("src/Turbo.Auth/Resources/merge/feature-access.sql");
            else if (missing.Any(item => item.StartsWith("FeatureAccessTasks.", StringComparison.Ordinal)))
                scripts.Add("src/Turbo.Auth/Resources/merge/apimart-task-route-upgrade.sql");
            if (missing.Any(item => item.StartsWith("Roles.") || item.StartsWith("Roles unique") || item.StartsWith("AccountRoles unique")))
                scripts.Add("src/Turbo.Auth/Resources/merge/role-system-upgrade.sql");
            if (missing.Contains("ModelKeyBinds.RoutingWeight"))
                scripts.Add("src/Turbo.Auth/Resources/merge/routing-weight-upgrade.sql");
            if (missing.Any(item => item != "ModelKeyBinds.RoutingWeight" && !item.StartsWith("Roles") && !item.StartsWith("AccountRoles") && !item.StartsWith("FeatureAccessTasks", StringComparison.Ordinal) && !item.StartsWith("RegistrationEmailChallenges", StringComparison.Ordinal)))
                scripts.Add("src/Turbo.Auth/Resources/merge/architecture-upgrade.sql");
            if (missing.Any(item => item.StartsWith("RegistrationEmailChallenges", StringComparison.Ordinal)))
                scripts.Add("src/Turbo.Auth/Resources/merge/registration-email-verification.sql");
            throw new InvalidOperationException("数据库架构尚未升级，缺少：" + string.Join(", ", missing) +
                "。请停止写入并备份；已有库对照 " + string.Join("、", scripts) + " 完成迁移。" +
                "部分升级的数据库需先核对实际结构，不能盲目整份重跑。空库请使用 merge/init.sql。详见 docs/deployment.md。启动不会自动修改数据库。");
        }
    }
}

public sealed class DatabaseSchemaValidationService(IConfiguration configuration,
    Microsoft.Extensions.Options.IOptions<Turbo.Auth.Security.Registration.RegistrationOptions> registration,
    Microsoft.Extensions.Options.IOptions<Turbo.Auth.Security.PasswordReset.PasswordResetOptions> smtp) : IHostedService
{
    public async Task StartAsync(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await ArchitectureSchemaCheck.Validate(configuration.GetConnectionString("ciko")!, timeout.Token, registration.Value.Enabled && smtp.Value.IsSmtpValid());
    }
    public Task StopAsync(CancellationToken token) => Task.CompletedTask;
}
