using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Turbo_Auth.Handlers.Model2Key;

public sealed class AiRoutingOptions
{
    public int FailureThreshold { get; init; } = 3;
    public int BreakDurationSeconds { get; init; } = 60;
}

public interface IRouteHealthTracker
{
    bool IsAvailable(int routeId);
    void RecordSuccess(int routeId);
    void RecordFailure(int routeId);
}

public sealed class RouteHealthTracker : IRouteHealthTracker
{
    private sealed class RouteHealth
    {
        public object Gate { get; } = new();
        public int ConsecutiveFailures { get; set; }
        public DateTimeOffset? OpenUntil { get; set; }
    }

    private readonly ConcurrentDictionary<int, RouteHealth> _routes = new();
    private readonly int _failureThreshold;
    private readonly TimeSpan _breakDuration;

    public RouteHealthTracker(IOptions<AiRoutingOptions> options)
    {
        _failureThreshold = Math.Max(1, options.Value.FailureThreshold);
        _breakDuration = TimeSpan.FromSeconds(Math.Max(1, options.Value.BreakDurationSeconds));
    }

    public bool IsAvailable(int routeId)
    {
        if (routeId <= 0 || !_routes.TryGetValue(routeId, out var health))
        {
            return true;
        }

        lock (health.Gate)
        {
            if (health.OpenUntil is not { } openUntil)
            {
                return true;
            }

            if (openUntil > DateTimeOffset.UtcNow)
            {
                return false;
            }

            health.OpenUntil = null;
            health.ConsecutiveFailures = 0;
            return true;
        }
    }

    public void RecordSuccess(int routeId)
    {
        if (routeId > 0)
        {
            _routes.TryRemove(routeId, out _);
        }
    }

    public void RecordFailure(int routeId)
    {
        if (routeId <= 0)
        {
            return;
        }

        var health = _routes.GetOrAdd(routeId, _ => new RouteHealth());
        lock (health.Gate)
        {
            health.ConsecutiveFailures++;
            if (health.ConsecutiveFailures >= _failureThreshold)
            {
                health.OpenUntil = DateTimeOffset.UtcNow.Add(_breakDuration);
            }
        }
    }
}
