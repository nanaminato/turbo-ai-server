using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace RelaxKon.Auth.Data;

/// <summary>One explicit provider selection for EF and raw persistence. MySQL is the compatibility default.</summary>
public sealed class DatabaseProvider
{
    private DatabaseProvider(bool isSqlite, string connectionString)
    {
        IsSqlite = isSqlite;
        ConnectionString = connectionString;
    }

    public bool IsSqlite { get; }
    public string ConnectionString { get; }

    public static DatabaseProvider FromConfiguration(IConfiguration configuration)
    {
        var name = configuration["Database:Provider"]?.Trim() ?? "MySql";
        var isSqlite = string.Equals(name, "Sqlite", StringComparison.OrdinalIgnoreCase);
        if (!isSqlite && !string.Equals(name, "MySql", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Database:Provider must be MySql or Sqlite.");
        var connectionString = configuration.GetConnectionString("ciko");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:ciko must be configured through environment-specific configuration or a secret store.");
        if (!isSqlite) return new(false, connectionString);

        var settings = new SqliteConnectionStringBuilder(connectionString);
        // A relative path, memory/URI connection, or separate per-replica database can silently
        // split identities and quotas. Runtime never creates an empty database on a typo.
        if (string.IsNullOrWhiteSpace(settings.DataSource) || !Path.IsPathFullyQualified(settings.DataSource)
            || settings.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            || settings.Mode == SqliteOpenMode.Memory)
            throw new InvalidOperationException("SQLite requires an absolute path to a persistent local database file. Memory databases and URI paths are not supported.");
        if (settings.Mode == SqliteOpenMode.ReadOnly)
            throw new InvalidOperationException("SQLite requires a writable database for accounts, sessions and quotas.");
        settings.Mode = SqliteOpenMode.ReadWrite;
        settings.ForeignKeys = true;
        settings.Cache = SqliteCacheMode.Private;
        settings.DefaultTimeout = Math.Clamp(settings.DefaultTimeout, 1, 60);
        return new(true, settings.ConnectionString);
    }

    public DbConnection CreateConnection() => IsSqlite
        ? new SqliteConnection(ConnectionString) : new MySqlConnection(ConnectionString);

    public void Configure(DbContextOptionsBuilder options)
    {
        if (IsSqlite) options.UseSqlite(ConnectionString);
        else options.UseMySql(ConnectionString, new MySqlServerVersion(new Version(8, 4, 6)));
    }

    public string Choose(string mysql, string sqlite) => IsSqlite ? sqlite : mysql;

    public DbCommand Command(DbConnection connection, DbTransaction? transaction, string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    public async Task<DbTransaction> BeginWrite(DbConnection connection, CancellationToken token,
        IsolationLevel isolationLevel = IsolationLevel.Unspecified)
    {
        token.ThrowIfCancellationRequested();
        // IMMEDIATE reserves SQLite's single writer before any policy/counter/identity read.
        // This lock belongs to the database, not to this process. BUSY/timeout fails closed.
        if (connection is SqliteConnection sqlite) return sqlite.BeginTransaction(deferred: false);
        return await connection.BeginTransactionAsync(isolationLevel, token);
    }

    public async Task<DbTransaction> BeginRead(DbConnection connection, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // WAL readers see one committed snapshot and must not reserve the writer lock.
        if (connection is SqliteConnection sqlite) return sqlite.BeginTransaction(deferred: true);
        return await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
    }
}
