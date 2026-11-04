using System.Runtime.CompilerServices;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;

namespace Turbo.Auth.Application.Chat;

public interface IChatService
{
    IAsyncEnumerable<string> Stream(ChatRequest request, CancellationToken cancellationToken);
}

public sealed class ChatService(IChatHandlerObtain handlers, QuickModel routes, IRouteHealthTracker health,
    PlayMixModelBacker backer, ILogger<ChatService> logger) : IChatService
{
    public async IAsyncEnumerable<string> Stream(ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var failed = new HashSet<int>();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ModelKey route;
            try { route = routes.GetModelAndKey(request.Model!, failed); }
            catch (KeyNotFoundException exception) when (failed.Count > 0)
            { throw new InvalidOperationException("All candidate AI routes failed.", exception); }
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
                emitted = true;
                yield return stream.Current;
            }
            if (retry) continue;
            health.RecordSuccess(route.RouteId);
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
