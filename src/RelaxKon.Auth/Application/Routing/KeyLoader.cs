using System.Data;
using Microsoft.EntityFrameworkCore;
using RelaxKon.Auth.Data.Contexts;

namespace RelaxKon.Auth.Application.Routing;

public sealed class KeyLoader(KeyContext database, IKeyPoolRepository pool, RouteReloadGate gate) : IKeyLoader
{
    public async Task LoadKeys(CancellationToken cancellationToken = default)
    {
        await gate.Mutex.WaitAsync(cancellationToken);
        try
        {
            database.Database.SetCommandTimeout(5);
            await using var transaction = await database.Database.BeginTransactionAsync(database.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true
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
        finally { gate.Mutex.Release(); }
    }
}
