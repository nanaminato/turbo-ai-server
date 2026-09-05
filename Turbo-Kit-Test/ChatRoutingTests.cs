using Turbo_Auth.Handlers.Builder;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Suppliers;

namespace Turbo_Kit_Test;

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
                Model = new Model { Enable = true, ModelValue = "free-model", Name = "Free model" }
            }
        ];

        var routes = await new ModelKeyBuilder().Build([key]);
        var activeRoutes = new QuickModel();
        activeRoutes.Transfer(routes);

        Assert.That(activeRoutes.IsModelAvailable("free-model"), Is.True);
        Assert.That(activeRoutes.GetModelAndKey("free-model").SupplierKey, Is.SameAs(key));
    }
}
