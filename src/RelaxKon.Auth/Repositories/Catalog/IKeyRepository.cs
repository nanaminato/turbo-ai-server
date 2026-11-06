using RelaxKon.Auth.Models.Suppliers;

namespace RelaxKon.Auth.Repositories.Catalog;

public interface IKeyRepository
{
    Task<List<SupplierKey>?> GetKeysAsync();
    Task<List<SupplierKey>?> GetKeysWithModelAsync(int modelId);
    Task<SupplierKey?> GetKeyByIdAsync(int id);
    Task DeleteKeyByIdAsync(int id);
    Task AddKeyAsync(SupplierKey key);
    Task UpdateKeyAsync(SupplierKey key);
}