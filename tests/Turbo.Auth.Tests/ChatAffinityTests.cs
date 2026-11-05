using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Auth.Application.Chat;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Models.Suppliers;

namespace Turbo.Auth.Tests;

public class ChatAffinityTests
{
    [Test]
    public void Healthy_affinity_survives_primary_recovery_and_respects_failed_route_exclusions()
    {
        var routes = Routes();
        var fallback = routes.GetModelKeys("test").Single(route => route.RouteId == 2);
        var affinity = RouteAffinity.From(fallback);
        Assert.That(routes.GetModelAndKey("test", affinity: affinity).RouteId, Is.EqualTo(2));
        Assert.That(routes.GetModelAndKey("test", new HashSet<int> { 2 }, affinity).RouteId, Is.EqualTo(1));
    }

    [Test]
    public void Unhealthy_or_deleted_affinity_does_not_override_available_routes()
    {
        var health = new FakeHealth();
        var routes = Routes(health);
        var affinity = RouteAffinity.From(routes.GetModelKeys("test").Single(route => route.RouteId == 2));
        health.Unavailable.Add(2);
        Assert.That(routes.GetModelAndKey("test", affinity: affinity).RouteId, Is.EqualTo(1));
        health.Unavailable.Clear();
        routes.Transfer(new QuickModel { Quick = new() { ["test"] = [Weight(1, 0)] } });
        Assert.That(routes.GetModelAndKey("test", affinity: affinity).RouteId, Is.EqualTo(1));
    }

    [Test]
    public void Changing_a_routes_credentials_invalidates_its_existing_affinity()
    {
        var routes = Routes();
        var affinity = RouteAffinity.From(routes.GetModelKeys("test").Single(route => route.RouteId == 2));
        var changed = Weight(2, 1);
        changed.SupplierKey!.ApiKey = "changed-secret";
        routes.Transfer(new QuickModel { Quick = new() { ["test"] = [Weight(1, 0), changed] } });
        Assert.That(routes.GetModelAndKey("test", affinity: affinity).RouteId, Is.EqualTo(1));
    }

    [Test]
    public async Task Successful_fallback_becomes_the_preferred_route_for_the_next_turn()
    {
        var routes = Routes();
        var store = new FakeAffinityStore();
        var handler = new RecordingHandler { FailPrimary = true };
        var service = Service(routes, handler, store);
        var request = new ChatRequest { Model = "test", AccountId = 7, ConversationId = "conversation" };
        Assert.That(await Collect(service, request), Is.EqualTo("reply"));
        Assert.That(store.Current?.RouteId, Is.EqualTo(2));
        handler.FailPrimary = false;
        Assert.That(await Collect(service, request), Is.EqualTo("reply"));
        Assert.That(handler.RouteIds, Is.EqualTo(new[] { 1, 2, 2 }));
        Assert.That(store.Writes, Is.EqualTo(2));
    }

    [Test]
    public async Task Legacy_or_unscoped_clients_do_not_read_or_write_affinity()
    {
        var store = new FakeAffinityStore();
        var service = Service(Routes(), new RecordingHandler(), store);
        await Collect(service, new ChatRequest { Model = "test", AccountId = 7 });
        await Collect(service, new ChatRequest { Model = "test", ConversationId = "conversation" });
        Assert.That(store.Reads, Is.Zero);
        Assert.That(store.Writes, Is.Zero);
    }

    [Test]
    public void Cancellation_does_not_save_an_unfinished_route()
    {
        var store = new FakeAffinityStore();
        var service = Service(Routes(), new RecordingHandler { Cancel = true }, store);
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await Collect(service, new ChatRequest { Model = "test", AccountId = 7, ConversationId = "conversation" }));
        Assert.That(store.Writes, Is.Zero);
    }

    [Test]
    public async Task Distributed_affinity_is_shared_between_instances_and_scoped_by_account_conversation_and_model()
    {
        using var services = new ServiceCollection().AddDistributedMemoryCache().BuildServiceProvider();
        var cache = services.GetRequiredService<IDistributedCache>();
        var options = Microsoft.Extensions.Options.Options.Create(new AiRoutingOptions());
        var first = new DistributedChatRouteAffinityStore(cache, options, NullLogger<DistributedChatRouteAffinityStore>.Instance);
        var second = new DistributedChatRouteAffinityStore(cache, options, NullLogger<DistributedChatRouteAffinityStore>.Instance);
        var affinity = RouteAffinity.From(Routes().GetModelAndKey("test"));
        await first.Write(7, "conversation", "test", affinity, default);
        Assert.That(await second.Read(7, "conversation", "test", default), Is.EqualTo(affinity));
        Assert.That(await second.Read(8, "conversation", "test", default), Is.Null);
        Assert.That(await second.Read(7, "different", "test", default), Is.Null);
        Assert.That(await second.Read(7, "conversation", "different", default), Is.Null);
    }

    private static ChatService Service(QuickModel routes, RecordingHandler handler, IChatRouteAffinityStore store) =>
        new(new ChatHandlerObtain([handler]), routes, new FakeHealth(), new PlayMixModelBacker(), NullLogger<ChatService>.Instance, store);

    private static async Task<string> Collect(ChatService service, ChatRequest request)
    {
        var chunks = new List<string>();
        await foreach (var chunk in service.Stream(request, default)) chunks.Add(chunk);
        return string.Concat(chunks);
    }

    private static QuickModel Routes(IRouteHealthTracker? health = null)
    {
        var routes = health is null ? new QuickModel() : new QuickModel(health);
        routes.Transfer(new QuickModel { Quick = new() { ["test"] = [Weight(1, 0), Weight(2, 1)] } });
        return routes;
    }

    private static WeightKey Weight(int id, int priority) => new()
    {
        RouteId = id, Priority = priority, Weight = 1, ProviderModelValue = "upstream",
        SupplierKey = new SupplierKey { SupplierKeyId = id, ApiKey = "test-secret", BaseUrl = "https://provider.invalid", RequestIdentifier = (int)HandlerType.Openai }
    };

    private sealed class FakeHealth : IRouteHealthTracker
    {
        public HashSet<int> Unavailable { get; } = [];
        public bool IsAvailable(int routeId) => !Unavailable.Contains(routeId);
        public void RecordSuccess(int routeId) { }
        public void RecordFailure(int routeId) { }
    }

    private sealed class RecordingHandler : IChatHandler
    {
        public HandlerType ProviderType => HandlerType.Openai;
        public bool FailPrimary { get; set; }
        public bool Cancel { get; init; }
        public List<int> RouteIds { get; } = [];
        public async IAsyncEnumerable<string> Stream(ChatRequest request, ModelKey route, [EnumeratorCancellation] CancellationToken token)
        {
            RouteIds.Add(route.RouteId);
            await Task.Yield();
            if (Cancel) throw new OperationCanceledException();
            if (FailPrimary && route.RouteId == 1) throw new InvalidOperationException("upstream failed");
            yield return "reply";
        }
    }

    private sealed class FakeAffinityStore : IChatRouteAffinityStore
    {
        public RouteAffinity? Current { get; private set; }
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public Task<RouteAffinity?> Read(int accountId, string conversationId, string model, CancellationToken token)
        { Reads++; return Task.FromResult(Current); }
        public Task Write(int accountId, string conversationId, string model, RouteAffinity route, CancellationToken token)
        { Writes++; Current = route; return Task.CompletedTask; }
    }
}

