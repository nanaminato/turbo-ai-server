using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RelaxKon.Auth.Data.Contexts;

namespace RelaxKon.Auth.Application.Routing;

public sealed class KeyLoader(KeyContext database, IKeyPoolRepository pool, RouteReloadGate gate) : IKeyLoader
{
    public async Task LoadKeys(CancellationToken cancellationToken = default)
    {
        await gate.Mutex.WaitAsync(cancellationToken);
        SqliteTransaction? sqliteSnapshot = null;
        var sqliteConnectionOpened = false;
        try
        {
            database.Database.SetCommandTimeout(5);
            if (database.Database.IsSqlite())
            {
                await database.Database.OpenConnectionAsync(cancellationToken);
                sqliteConnectionOpened = true;
                // A routing poll is read-only. A deferred WAL snapshot sees a consistent
                // revision/catalog without reserving SQLite's single writer every poll.
                sqliteSnapshot = ((SqliteConnection)database.Database.GetDbConnection()).BeginTransaction(deferred: true);
            }
            await using var transaction = sqliteSnapshot is not null
                ? (await database.Database.UseTransactionAsync(sqliteSnapshot, cancellationToken))!
                : await database.Database.BeginTransactionAsync(database.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true
                    ? IsolationLevel.RepeatableRead : IsolationLevel.Serializable, cancellationToken);
            var revision = await database.Database.SqlQuery<long>($"SELECT Version AS Value FROM RoutingConfiguration WHERE Id=1")
                .SingleAsync(cancellationToken);
            if (gate.LoadedRevision != revision)
            {
                var keys = await database.SupplierKeys!.AsNoTracking().Where(key => key.Enable)
                    .Include(key => key.ModelKeyBinds)!.ThenInclude(bind => bind.Model).ToListAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                await pool.Replace(keys);
                gate.LoadedRevision = revision;
            }
            else await transaction.CommitAsync(cancellationToken);
            gate.Checked();
        }
        finally
        {
            try
            {
                if (sqliteSnapshot is not null) await sqliteSnapshot.DisposeAsync();
                if (sqliteConnectionOpened) await database.Database.CloseConnectionAsync();
            }
            finally { gate.Mutex.Release(); }
        }
    }
}
