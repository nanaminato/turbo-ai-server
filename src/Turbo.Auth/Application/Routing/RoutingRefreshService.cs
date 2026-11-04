using Microsoft.Extensions.Options;

namespace Turbo.Auth.Application.Routing;

public sealed class RoutingRefreshService(IServiceScopeFactory scopes, RouteReloadGate gate, QuickModel routes,
    IOptions<AiRoutingOptions> options, ILogger<RoutingRefreshService> logger) : BackgroundService
{
    public override async Task StartAsync(CancellationToken token)
    {
        // Initial load must succeed before accepting traffic.
        await Refresh(token);
        await base.StartAsync(token);
    }
    private async Task Refresh(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IKeyLoader>().LoadKeys(timeout.Token);
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Clamp(options.Value.RefreshIntervalSeconds, 1, 60));
        var maxStale = TimeSpan.FromSeconds(Math.Max(10, options.Value.MaxStaleSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await Refresh(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                logger.LogError("Routing refresh failed. ExceptionType: {ExceptionType}", exception.GetType().Name);
                await gate.Expire(routes, maxStale, stoppingToken);
            }
        }
    }
}
