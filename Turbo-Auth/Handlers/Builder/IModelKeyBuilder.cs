using Turbo_Auth.Handlers.keyPool;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Suppliers;

namespace Turbo_Auth.Handlers.Builder;

public interface IModelKeyBuilder
{
    public Task<QuickModel> Build(List<SupplierKey> supplierKeys);
}