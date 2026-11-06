using System.Net.Mail;
using System.Text.RegularExpressions;
using MySqlConnector;
using Microsoft.Data.Sqlite;
using Turbo.Auth.Data;
using Turbo.Auth.Models.Accounts;
using Turbo.Auth.Security;

namespace Turbo.Auth.Bootstrap;

public sealed record BootstrapAdmin(string Username, string Email, string Password)
{
    // Keep passwords out of record-generated diagnostics.
    public override string ToString() => Username;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Username) || Username.Length is < 3 or > 20)
            throw new ArgumentException("管理员用户名须为 3–20 个字符。");
        if (string.IsNullOrWhiteSpace(Email) || Email.Length > 50 || !MailAddress.TryCreate(Email, out var address) || address.Address != Email)
            throw new ArgumentException("请输入有效邮箱（最多 50 个字符）。");
        if (string.IsNullOrWhiteSpace(Password) || Password.Length is < 6 or > 20)
            throw new ArgumentException("管理员密码须为 6–20 个字符。");
    }
}

/// <summary>Explicit maintenance commands only; never called during normal web startup.</summary>
public sealed class DatabaseBootstrapper
{
    public static string ValidateDatabaseName(string database)
    {
        if (string.IsNullOrWhiteSpace(database) || database.Length > 64 ||
            !Regex.IsMatch(database, "^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant) ||
            new[] { "mysql", "sys", "information_schema", "performance_schema" }.Contains(database, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("数据库名须为 1–64 个字母、数字或下划线，且不能是系统数据库。");
        return database;
    }

    public static string ReadScript(string name)
    {
        using var stream = typeof(DatabaseBootstrapper).Assembly.GetManifestResourceStream($"Turbo.Auth.Bootstrap.{name}.sql")
            ?? throw new InvalidOperationException("初始化 SQL 资源缺失。");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public async Task<int> RunAsync(string connectionString, BootstrapAdmin admin, bool initialize,
        CancellationToken cancellationToken = default)
    {
        admin.Validate();
        var settings = new MySqlConnectionStringBuilder(connectionString);
        var database = ValidateDatabaseName(settings.Database);
        settings.Database = "";
        await using var connection = new MySqlConnection(settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        // The connection-scoped advisory lock serializes competing bootstrap commands.
        var lockName = "turbo-bootstrap-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(database.ToLowerInvariant())))[..40];
        await using var getLock = new MySqlCommand("SELECT GET_LOCK(@name, 10)", connection);
        getLock.Parameters.AddWithValue("@name", lockName);
        if (Convert.ToInt32(await getLock.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new InvalidOperationException("其他初始化命令正在执行，请稍后重试。");
        try
        {
            if (initialize)
            {
                // Check before CREATE/DDL. MySQL DDL cannot be rolled back.
                await using var count = new MySqlCommand(
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = @database", connection);
                count.Parameters.AddWithValue("@database", database);
                if (Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken)) != 0)
                    throw new InvalidOperationException("拒绝初始化非空数据库；已有数据库请使用升级脚本或 --create-admin。");
                await using var create = new MySqlCommand(
                    $"CREATE DATABASE IF NOT EXISTS `{database}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci", connection);
                await create.ExecuteNonQueryAsync(cancellationToken);
                await connection.ChangeDatabaseAsync(database, cancellationToken);
                foreach (var name in new[] { "Schema", "Seed" })
                {
                    await using var script = new MySqlCommand(ReadScript(name), connection) { CommandTimeout = 120 };
                    await script.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            else
            {
                await connection.ChangeDatabaseAsync(database, cancellationToken);
            }
            return await CreateAdminAsync(connection, admin, cancellationToken);
        }
        finally
        {
            await using var release = new MySqlCommand("SELECT RELEASE_LOCK(@name)", connection);
            release.Parameters.AddWithValue("@name", lockName);
            await release.ExecuteScalarAsync(CancellationToken.None);
        }
    }

    public Task<int> RunAsync(DatabaseProvider provider, BootstrapAdmin admin, bool initialize,
        CancellationToken cancellationToken = default) => provider.IsSqlite
        ? RunSqliteAsync(provider.ConnectionString, admin, initialize, cancellationToken)
        : RunAsync(provider.ConnectionString, admin, initialize, cancellationToken);

    private static async Task<int> RunSqliteAsync(string connectionString, BootstrapAdmin admin, bool initialize,
        CancellationToken cancellationToken)
    {
        admin.Validate();
        var settings = new SqliteConnectionStringBuilder(connectionString)
        {
            Mode = initialize ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
            ForeignKeys = true
        };
        if (string.IsNullOrWhiteSpace(settings.DataSource) || settings.DataSource == ":memory:" ||
            !Path.IsPathFullyQualified(settings.DataSource))
            throw new ArgumentException("SQLite 初始化需要绝对数据库文件路径。请设置 ConnectionStrings__ciko=Data Source=/absolute/path/relaxkon.db。");
        if (initialize && !File.Exists(settings.DataSource))
        {
            // SQLite's usual file creation inherits umask (often 022). Create only a new
            // file with owner-only permissions; never chmod or replace an existing file.
            var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite };
            if (!OperatingSystem.IsWindows())
                fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            try { using var created = new FileStream(settings.DataSource, fileOptions); }
            catch (IOException) when (File.Exists(settings.DataSource))
            {
                // A concurrent initializer may have won CreateNew. The locked schema
                // check below decides whether this command can proceed without overwrite.
            }
        }
        await using var connection = new SqliteConnection(settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        if (initialize)
        {
            // Do not change the journal of an existing database. WAL must be selected before
            // schema/admin creation because SQLite cannot switch it inside that transaction.
            await using var count = new SqliteCommand(
                "SELECT COUNT(*) FROM sqlite_schema WHERE name NOT GLOB 'sqlite_*'", connection);
            if (Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken)) != 0)
                throw new InvalidOperationException("拒绝初始化非空数据库；已有数据库请使用升级脚本或 --create-admin。");
            await using var journal = new SqliteCommand("PRAGMA journal_mode=WAL", connection);
            var mode = Convert.ToString(await journal.ExecuteScalarAsync(cancellationToken));
            if (!string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SQLite 无法启用 WAL；尚未创建架构或管理员。请检查文件系统是否支持 SQLite WAL。");
            // A competing initializer may finish after the preliminary check. It also selects
            // WAL; the repeated check under the immediate write lock below prevents overwrites.
        }
        // An immediate transaction serializes competing initializers and rolls back all DDL and seed data on failure.
        await using var transaction = connection.BeginTransaction(deferred: false);
        if (initialize)
        {
            await using var count = new SqliteCommand(
                "SELECT COUNT(*) FROM sqlite_schema WHERE name NOT GLOB 'sqlite_*'", connection, transaction);
            if (Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken)) != 0)
                throw new InvalidOperationException("拒绝初始化非空数据库；已有数据库请使用升级脚本或 --create-admin。");
            foreach (var name in new[] { "SqliteSchema", "SqliteSeed" })
            {
                await using var script = new SqliteCommand(ReadScript(name), connection, transaction) { CommandTimeout = 120 };
                await script.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        var id = await CreateSqliteAdminAsync(connection, admin, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return id;
    }

    private static async Task<int> CreateSqliteAdminAsync(SqliteConnection connection, BootstrapAdmin admin, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var check = new SqliteCommand("""
            SELECT COUNT(*) FROM Accounts a
            WHERE a.Username = @username OR a.Email = @email OR EXISTS (
                SELECT 1 FROM AccountRoles ar JOIN Roles r ON r.RoleId = ar.RoleId
                WHERE ar.AccountId = a.AccountId AND r.Code = 'admin')
            """, connection, transaction);
        check.Parameters.AddWithValue("@username", admin.Username);
        check.Parameters.AddWithValue("@email", admin.Email);
        if (Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken)) != 0)
            throw new InvalidOperationException("管理员已存在，或用户名/邮箱已被使用；不会覆盖账户或密码。");

        var roles = new Dictionary<string, int>(StringComparer.Ordinal);
        await using (var query = new SqliteCommand("SELECT RoleId, Code FROM Roles WHERE Code IN ('admin','user','vip')", connection, transaction))
        await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!roles.TryAdd(reader.GetString(1), reader.GetInt32(0)))
                    throw new InvalidOperationException("角色数据重复，请先修复 Roles 表。");
            }
        }
        if (roles.Count != 3) throw new InvalidOperationException("请先导入基础角色数据。");
        var account = new Account { Username = admin.Username, Email = admin.Email };
        account.Password = new AccountPasswordService().Hash(account, admin.Password);
        await using var insert = new SqliteCommand("""
            INSERT INTO Accounts (Username, Email, Password, SecurityStamp) VALUES (@username,@email,@hash,@stamp)
            """, connection, transaction);
        insert.Parameters.AddWithValue("@username", account.Username);
        insert.Parameters.AddWithValue("@email", account.Email);
        insert.Parameters.AddWithValue("@hash", account.Password);
        insert.Parameters.AddWithValue("@stamp", account.SecurityStamp);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await using var identity = new SqliteCommand("SELECT last_insert_rowid()", connection, transaction);
        var id = checked((int)Convert.ToInt64(await identity.ExecuteScalarAsync(cancellationToken)));
        foreach (var roleId in roles.Values)
        {
            await using var bind = new SqliteCommand("INSERT INTO AccountRoles (AccountId,RoleId) VALUES (@account,@role)", connection, transaction);
            bind.Parameters.AddWithValue("@account", id);
            bind.Parameters.AddWithValue("@role", roleId);
            await bind.ExecuteNonQueryAsync(cancellationToken);
        }
        return id;
    }

    private static async Task<int> CreateAdminAsync(MySqlConnection connection, BootstrapAdmin admin, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var check = new MySqlCommand("""
            SELECT COUNT(*) FROM Accounts a
            WHERE a.Username = @username OR a.Email = @email OR EXISTS (
                SELECT 1 FROM AccountRoles ar JOIN Roles r ON r.RoleId = ar.RoleId
                WHERE ar.AccountId = a.AccountId AND r.Code = 'admin')
            """, connection, transaction);
        check.Parameters.AddWithValue("@username", admin.Username);
        check.Parameters.AddWithValue("@email", admin.Email);
        if (Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken)) != 0)
            throw new InvalidOperationException("管理员已存在，或用户名/邮箱已被使用；不会覆盖账户或密码。");

        var roles = new Dictionary<string, int>(StringComparer.Ordinal);
        await using (var query = new MySqlCommand("SELECT RoleId, Code FROM Roles WHERE Code IN ('admin','user','vip')", connection, transaction))
        await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!roles.TryAdd(reader.GetString(1), reader.GetInt32(0)))
                    throw new InvalidOperationException("角色数据重复，请先修复 Roles 表。");
            }
        }
        if (roles.Count != 3) throw new InvalidOperationException("请先导入基础角色数据。");
        var account = new Account { Username = admin.Username, Email = admin.Email };
        account.Password = new AccountPasswordService().Hash(account, admin.Password);
        await using var insert = new MySqlCommand("""
            INSERT INTO Accounts (Username, Email, Password, SecurityStamp) VALUES (@username,@email,@hash,@stamp)
            """, connection, transaction);
        insert.Parameters.AddWithValue("@username", account.Username);
        insert.Parameters.AddWithValue("@email", account.Email);
        insert.Parameters.AddWithValue("@hash", account.Password);
        insert.Parameters.AddWithValue("@stamp", account.SecurityStamp);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        var id = checked((int)insert.LastInsertedId);
        foreach (var roleId in roles.Values)
        {
            await using var bind = new MySqlCommand("INSERT INTO AccountRoles (AccountId,RoleId) VALUES (@account,@role)", connection, transaction);
            bind.Parameters.AddWithValue("@account", id);
            bind.Parameters.AddWithValue("@role", roleId);
            await bind.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return id;
    }
}
