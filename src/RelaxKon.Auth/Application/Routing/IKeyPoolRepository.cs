using RelaxKon.Auth.Models.Suppliers;

namespace RelaxKon.Auth.Application.Routing;

public interface IKeyPoolRepository
{
    public Task Replace(List<SupplierKey> supplierKeys);
}