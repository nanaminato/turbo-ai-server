namespace RelaxKon.Auth.Application.Routing;

/// <summary>Serializes publication inside one replica; revisions are shared in MySQL.</summary>
public sealed class RouteReloadGate(TimeProvider clock)
{
    public SemaphoreSlim Mutex { get; } = new(1, 1);
    public long? LoadedRevision { get; set; }
    public DateTimeOffset LastChecked { get; set; } = DateTimeOffset.MinValue;
    public void Checked() => LastChecked = clock.GetUtcNow();

    public async Task Expire(QuickModel routes, TimeSpan maxStale, CancellationToken token)
    {
        await Mutex.WaitAsync(token);
        try
        {
            if (clock.GetUtcNow() - LastChecked < maxStale) return;
            routes.Transfer(new QuickModel());
            LoadedRevision = null; // Reload even if the database revision has not changed on recovery.
        }
        finally { Mutex.Release(); }
    }
}
