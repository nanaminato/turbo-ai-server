using System.Runtime.CompilerServices;
using RelaxKon.Auth.Application.Providers;
using RelaxKon.Auth.Application.Routing;

namespace RelaxKon.Auth.Application.Chat;

public interface IChatService
{
    IAsyncEnumerable<string> Stream(ChatRequest request, CancellationToken cancellationToken);
}

public sealed class ChatService(IChatHandlerObtain handlers, QuickModel routes, IRouteHealthTracker health,
    PlayMixModelBacker backer, ILogger<ChatService> logger, IChatRouteAffinityStore? affinity = null) : IChatService
{
    public async IAsyncEnumerable<string> Stream(ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        double? firstTokenMilliseconds = null;
        var failed = new HashSet<int>();
        var hasAffinity = affinity is not null && request.AccountId is > 0 &&
            !string.IsNullOrWhiteSpace(request.ConversationId) && request.ConversationId.Length <= 128;
        var binding = hasAffinity ? await affinity!.Read(request.AccountId!.Value,
            request.ConversationId!, request.Model!, cancellationToken) : null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ModelKey route;
            try { route = routes.GetModelAndKey(request.Model!, failed, binding); }
            catch (KeyNotFoundException exception) when (failed.Count > 0)
            { throw new InvalidOperationException("All candidate AI routes failed.", exception); }
            var identity = RouteAffinity.From(route);
            route.Model = backer.Backer(route.Model!);
            await using var stream = StreamRoute(request, route, cancellationToken).GetAsyncEnumerator(cancellationToken);
            var emitted = false;
            var retry = false;
            while (true)
            {
                bool hasNext;
                try { hasNext = await stream.MoveNextAsync(); }
                catch (OperationCanceledException) { throw; }
                catch (Exception exception)
                {
                    health.RecordFailure(route.RouteId);
                    if (emitted) throw;
                    failed.Add(route.RouteId);
                    logger.LogWarning("AI route failed before output. RouteId: {RouteId}; ExceptionType: {ExceptionType}",
                        route.RouteId, exception.GetType().Name);
                    retry = true;
                    break;
                }
                if (!hasNext) break;
                if (string.IsNullOrEmpty(stream.Current)) continue;
                firstTokenMilliseconds ??= timer.Elapsed.TotalMilliseconds;
                emitted = true;
                yield return stream.Current;
            }
            if (retry) continue;
            health.RecordSuccess(route.RouteId);
            if (hasAffinity) await affinity!.Write(request.AccountId!.Value, request.ConversationId!,
                request.Model!, identity, cancellationToken);
            logger.LogInformation("AI chat routing completed. RouteId: {RouteId}; LogicalModel: {LogicalModel}; AffinityReused: {AffinityReused}; FailoverCount: {FailoverCount}; FirstTokenMilliseconds: {FirstTokenMilliseconds}; DurationMilliseconds: {DurationMilliseconds}",
                route.RouteId, request.Model, binding == identity, failed.Count,
                firstTokenMilliseconds, timer.Elapsed.TotalMilliseconds);
            yield break;
        }
    }
    private async IAsyncEnumerable<string> StreamRoute(ChatRequest request, ModelKey route,
        [EnumeratorCancellation] CancellationToken token)
    {
        var handler = handlers.GetHandler((HandlerType)route.SupplierKey!.RequestIdentifier);
        await foreach (var text in handler.Stream(request, route, token)) yield return text;
    }

}
