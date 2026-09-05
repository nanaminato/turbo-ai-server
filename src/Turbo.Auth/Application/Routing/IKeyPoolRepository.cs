using Turbo.Auth.Models.Suppliers;

namespace Turbo.Auth.Application.Routing;

public interface IKeyPoolRepository
{
    public Task Replace(List<SupplierKey> supplierKeys);
}