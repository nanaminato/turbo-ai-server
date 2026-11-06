using RelaxKon.Auth.Application.Providers;
using RelaxKon.Auth.Application.Routing;
using RelaxKon.Auth.Models.Suppliers;

namespace RelaxKon.Auth.Application.Routing;

public class ModelKeyBuilder : IModelKeyBuilder
{
    public Task<QuickModel> Build(List<SupplierKey> supplierKeys)
    {
        var routes = supplierKeys
            .Where(key => key.Enable)
            .SelectMany(key => key.ModelKeyBinds ?? Enumerable.Empty<ModelKeyBind>())
            .Where(bind => bind.Enable && bind.Model is { ModelValue: not null })
            .Where(bind => bind.SupplierKey != null)
            .Where(bind => !(bind.Model!.ModelValue == "gpt-image-2" &&
                             bind.SupplierKey!.BaseUrl?.Contains("apimart", StringComparison.OrdinalIgnoreCase) == true))
            .GroupBy(bind => bind.Model!.ModelValue!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(bind => new WeightKey
                {
                    RouteId = bind.ModelKeyBindId,
                    Priority = bind.Priority,
                    ProviderModelValue = string.IsNullOrWhiteSpace(bind.ProviderModelValue)
                        ? bind.Model!.ModelValue
                        : bind.ProviderModelValue,
                    Weight = bind.RoutingWeight ?? (double.IsFinite(bind.Fee) && bind.Fee > 0
                        ? Math.Min(double.MaxValue, 1 / bind.Fee) : 1),
                    SupplierKey = bind.SupplierKey
                }).ToList(),
                StringComparer.Ordinal);

        var apiMartKeys = supplierKeys
            .Where(key => key.Enable && key.RequestIdentifier == (int)HandlerType.ApiMart)
            .ToList();

        return Task.FromResult(new QuickModel
        {
            Quick = routes,
            ApiMartKeys = apiMartKeys
        });
    }
}
