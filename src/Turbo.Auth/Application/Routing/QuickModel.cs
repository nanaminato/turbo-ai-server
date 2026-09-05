using Turbo.Auth.Models.Suppliers;

namespace Turbo.Auth.Application.Routing;

public class QuickModel
{
    private sealed record RouteSnapshot(
        IReadOnlyDictionary<string, IReadOnlyList<WeightKey>> Routes,
        IReadOnlyList<SupplierKey> ApiMartKeys)
    {
        public static readonly RouteSnapshot Empty = new(
            new Dictionary<string, IReadOnlyList<WeightKey>>(StringComparer.Ordinal),
            Array.Empty<SupplierKey>());
    }

    // Used only while a new route table is being built. Readers always use _snapshot.
    private Dictionary<string, List<WeightKey>> _quick = new(StringComparer.Ordinal);
    private RouteSnapshot _snapshot = RouteSnapshot.Empty;
    private readonly IRouteHealthTracker _routeHealthTracker;

    public QuickModel() : this(AlwaysAvailableRouteHealthTracker.Instance)
    {
    }

    public QuickModel(IRouteHealthTracker routeHealthTracker)
    {
        _routeHealthTracker = routeHealthTracker;
    }

    public List<SupplierKey>? ApiMartKeys { get; set; }

    public Dictionary<string, List<WeightKey>> Quick
    {
        get => _quick;
        set => _quick = value ?? new Dictionary<string, List<WeightKey>>(StringComparer.Ordinal);
    }

    public bool IsModelAvailable(string? model) =>
        !string.IsNullOrWhiteSpace(model) && Volatile.Read(ref _snapshot).Routes.ContainsKey(model);

    public SupplierKey? GetApiMartKey()
    {
        var keys = Volatile.Read(ref _snapshot).ApiMartKeys;
        return keys.Count == 0 ? null : keys[Random.Shared.Next(keys.Count)];
    }

    public IReadOnlyDictionary<string, IReadOnlyList<WeightKey>> GetQuick() =>
        Volatile.Read(ref _snapshot).Routes;

    public List<ModelKey> GetModelKeys(string model)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        if (!snapshot.Routes.TryGetValue(model, out var weights))
        {
            return [];
        }

        return weights.Select(weight => new ModelKey
        {
            LogicalModel = model,
            Model = weight.ProviderModelValue,
            RouteId = weight.RouteId,
            SupplierKey = weight.SupplierKey
        }).ToList();
    }

    public ModelKey GetModelAndKey(string model, ISet<int>? excludedRouteIds = null)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        if (!snapshot.Routes.TryGetValue(model, out var weights) || weights.Count == 0)
        {
            throw new KeyNotFoundException("当前数据库不存在支持当前模型的可用密钥。");
        }

        var availableWeights = weights
            .Where(weight => (excludedRouteIds == null || !excludedRouteIds.Contains(weight.RouteId)) &&
                             _routeHealthTracker.IsAvailable(weight.RouteId))
            .ToArray();
        if (availableWeights.Length == 0)
        {
            throw new KeyNotFoundException("当前模型没有健康的可用路由。");
        }

        var selected = WeightRandom(availableWeights);
        return new ModelKey
        {
            LogicalModel = model,
            Model = selected.ProviderModelValue,
            RouteId = selected.RouteId,
            SupplierKey = selected.SupplierKey
        };
    }

    public void Transfer(QuickModel quickModel)
    {
        var routes = new Dictionary<string, IReadOnlyList<WeightKey>>(StringComparer.Ordinal);
        foreach (var (model, weights) in quickModel.Quick)
        {
            var copiedWeights = weights
                .Where(weight => weight.SupplierKey != null && weight.Weight > 0)
                .Select(weight => new WeightKey
                {
                    RouteId = weight.RouteId,
                    Priority = weight.Priority,
                    ProviderModelValue = weight.ProviderModelValue,
                    Weight = weight.Weight,
                    SupplierKey = weight.SupplierKey
                })
                .ToArray();

            if (copiedWeights.Length > 0)
            {
                routes[model] = copiedWeights;
            }
        }

        var apiMartKeys = quickModel.ApiMartKeys?.ToArray() ?? [];
        Volatile.Write(ref _snapshot, new RouteSnapshot(routes, apiMartKeys));
    }

    private static WeightKey WeightRandom(IReadOnlyList<WeightKey> modelWeights)
    {
        var preferredPriority = modelWeights.Min(item => item.Priority);
        var preferredRoutes = modelWeights
            .Where(item => item.Priority == preferredPriority)
            .ToArray();
        var totalWeight = preferredRoutes.Sum(item => item.Weight);
        if (totalWeight <= 0)
        {
            throw new InvalidOperationException("模型没有可用的路由权重。");
        }

        var randomWeight = Random.Shared.NextDouble() * totalWeight;
        foreach (var item in preferredRoutes)
        {
            if (randomWeight < item.Weight)
            {
                return item;
            }

            randomWeight -= item.Weight;
        }

        return preferredRoutes[^1];
    }

    private sealed class AlwaysAvailableRouteHealthTracker : IRouteHealthTracker
    {
        public static readonly AlwaysAvailableRouteHealthTracker Instance = new();

        public bool IsAvailable(int routeId) => true;
        public void RecordSuccess(int routeId) { }
        public void RecordFailure(int routeId) { }
    }
}
