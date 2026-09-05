using Turbo.Auth.Application.Routing;
using Turbo.Auth.Models.Suppliers;
using Microsoft.Extensions.Options;

namespace Turbo.Auth.Tests;

public class ChatRoutingTests
{
    [Test]
    public void Transfer_publishes_a_complete_route_snapshot()
    {
        var activeRoutes = new QuickModel();
        var firstKey = new SupplierKey { ApiKey = "first", BaseUrl = "https://first.example" };
        var firstBuild = new QuickModel
        {
            Quick = new Dictionary<string, List<WeightKey>>
            {
                ["first-model"] = [new() { Weight = 1, SupplierKey = firstKey }]
            }
        };

        activeRoutes.Transfer(firstBuild);
        firstBuild.Quick.Clear();

        Assert.That(activeRoutes.IsModelAvailable("first-model"), Is.True);
        Assert.That(activeRoutes.GetModelAndKey("first-model").SupplierKey, Is.SameAs(firstKey));

        var secondKey = new SupplierKey { ApiKey = "second", BaseUrl = "https://second.example" };
        activeRoutes.Transfer(new QuickModel
        {
            Quick = new Dictionary<string, List<WeightKey>>
            {
                ["second-model"] = [new() { Weight = 1, SupplierKey = secondKey }]
            }
        });

        Assert.That(activeRoutes.IsModelAvailable("first-model"), Is.False);
        Assert.That(activeRoutes.IsModelAvailable("second-model"), Is.True);
    }

    [Test]
    public async Task Model_key_builder_keeps_enabled_zero_fee_routes_selectable()
    {
        var key = new SupplierKey { Enable = true, ApiKey = "key", BaseUrl = "https://api.example" };
        key.ModelKeyBinds =
        [
            new ModelKeyBind
            {
                Enable = true,
                Fee = 0,
                SupplierKey = key,
                Model = new Model { ModelValue = "free-model", Name = "Free model" }
            }
        ];

        var routes = await new ModelKeyBuilder().Build([key]);
        var activeRoutes = new QuickModel();
        activeRoutes.Transfer(routes);

        Assert.That(activeRoutes.IsModelAvailable("free-model"), Is.True);
        Assert.That(activeRoutes.GetModelAndKey("free-model").SupplierKey, Is.SameAs(key));
    }

    [Test]
    public async Task Model_key_builder_uses_provider_model_value_and_priority()
    {
        var primaryKey = CreateRouteKey(1, "primary-model", priority: 0, fee: 10);
        var fallbackKey = CreateRouteKey(2, "fallback-model", priority: 1, fee: 1);
        var routes = await new ModelKeyBuilder().Build([primaryKey, fallbackKey]);
        var activeRoutes = new QuickModel();
        activeRoutes.Transfer(routes);

        var selected = activeRoutes.GetModelAndKey("logical-model");

        Assert.That(selected.Model, Is.EqualTo("primary-model"));
        Assert.That(selected.LogicalModel, Is.EqualTo("logical-model"));
        Assert.That(selected.RouteId, Is.EqualTo(1));
    }

    [Test]
    public void Health_tracker_opens_a_route_after_consecutive_failures()
    {
        var tracker = new RouteHealthTracker(Microsoft.Extensions.Options.Options.Create(new AiRoutingOptions
        {
            FailureThreshold = 2,
            BreakDurationSeconds = 60
        }));

        tracker.RecordFailure(12);
        Assert.That(tracker.IsAvailable(12), Is.True);

        tracker.RecordFailure(12);
        Assert.That(tracker.IsAvailable(12), Is.False);

        tracker.RecordSuccess(12);
        Assert.That(tracker.IsAvailable(12), Is.True);
    }

    private static SupplierKey CreateRouteKey(int routeId, string providerModel, int priority, double fee)
    {
        var key = new SupplierKey { Enable = true, ApiKey = $"key-{routeId}", BaseUrl = "https://api.example" };
        key.ModelKeyBinds =
        [
            new ModelKeyBind
            {
                ModelKeyBindId = routeId,
                Enable = true,
                Fee = fee,
                Priority = priority,
                ProviderModelValue = providerModel,
                SupplierKey = key,
                Model = new Model { ModelValue = "logical-model", Name = "Logical model" }
            }
        ];
        return key;
    }
}
