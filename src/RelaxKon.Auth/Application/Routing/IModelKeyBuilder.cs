using RelaxKon.Auth.Application.Routing;
using RelaxKon.Auth.Models.Suppliers;

namespace RelaxKon.Auth.Application.Routing;

public interface IModelKeyBuilder
{
    public Task<QuickModel> Build(List<SupplierKey> supplierKeys);
}
