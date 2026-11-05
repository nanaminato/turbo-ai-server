using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Turbo.Auth.Application.Routing;

// The signature invalidates bindings when a route's actual cache domain may have changed.
// No API keys, prompts, account IDs or conversation IDs are stored in plaintext in Redis.
public sealed record RouteAffinity(int RouteId, string Signature)
{
    public static RouteAffinity From(ModelKey route) => new(route.RouteId,
        Hash(JsonSerializer.Serialize(new { route.Model, route.SupplierKey?.SupplierKeyId,
            route.SupplierKey?.RequestIdentifier, route.SupplierKey?.BaseUrl, route.SupplierKey?.ApiKey })));
    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public interface IChatRouteAffinityStore
{
    Task<RouteAffinity?> Read(int accountId, string conversationId, string model, CancellationToken token);
    Task Write(int accountId, string conversationId, string model, RouteAffinity route, CancellationToken token);
}

public sealed class DistributedChatRouteAffinityStore(IDistributedCache cache, IOptions<AiRoutingOptions> options,
    ILogger<DistributedChatRouteAffinityStore> logger) : IChatRouteAffinityStore
{
    private static string Key(int accountId, string conversationId, string model) =>
        "chat-affinity:v1:" + RouteAffinity.Hash(JsonSerializer.Serialize(new { accountId, conversationId, model }));

    public async Task<RouteAffinity?> Read(int accountId, string conversationId, string model, CancellationToken token)
    {
        if (!options.Value.AffinityEnabled) return null;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(1));
            var json = await cache.GetStringAsync(Key(accountId, conversationId, model), timeout.Token);
            return json is null ? null : JsonSerializer.Deserialize<RouteAffinity>(json);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning("Chat route affinity read unavailable. ExceptionType: {ExceptionType}", exception.GetType().Name);
            return null; // Routing is an optimization; a cache outage must not block chat.
        }
    }

    public async Task Write(int accountId, string conversationId, string model, RouteAffinity route, CancellationToken token)
    {
        if (!options.Value.AffinityEnabled) return;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(1));
            await cache.SetStringAsync(Key(accountId, conversationId, model), JsonSerializer.Serialize(route),
                new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(Math.Clamp(options.Value.AffinityMinutes, 1, 1440)) }, timeout.Token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning("Chat route affinity write unavailable. ExceptionType: {ExceptionType}", exception.GetType().Name);
        }
    }
}
