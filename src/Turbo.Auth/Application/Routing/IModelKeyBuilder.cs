using Turbo.Auth.Application.Routing;
using Turbo.Auth.Models.Suppliers;

namespace Turbo.Auth.Application.Routing;

public interface IModelKeyBuilder
{
    public Task<QuickModel> Build(List<SupplierKey> supplierKeys);
}
