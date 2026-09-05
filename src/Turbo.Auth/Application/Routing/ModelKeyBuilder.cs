using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Models.Suppliers;

namespace Turbo.Auth.Application.Routing;

public class ModelKeyBuilder : IModelKeyBuilder
{
    public Task<QuickModel> Build(List<SupplierKey> supplierKeys)
    {
        var routes = supplierKeys
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
                    Weight = bind.Fee > 0 ? 1 / bind.Fee : 1,
                    SupplierKey = bind.SupplierKey
                }).ToList(),
                StringComparer.Ordinal);

        var apiMartKeys = supplierKeys
            .Where(key => key.RequestIdentifier == (int)HandlerType.ApiMart)
            .ToList();

        return Task.FromResult(new QuickModel
        {
            Quick = routes,
            ApiMartKeys = apiMartKeys
        });
    }
}
