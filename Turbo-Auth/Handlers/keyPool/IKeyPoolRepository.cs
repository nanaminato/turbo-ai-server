using Turbo_Auth.Models.Suppliers;

namespace Turbo_Auth.Handlers.keyPool;

public interface IKeyPoolRepository
{
    public Task Replace(List<SupplierKey> supplierKeys);
}