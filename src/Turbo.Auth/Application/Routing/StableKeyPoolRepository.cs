using Turbo.Auth.Application.Routing;
using Turbo.Auth.Models.Suppliers;

namespace Turbo.Auth.Application.Routing;

public class StableKeyPoolRepository: IKeyPoolRepository
{
    private IReadOnlyList<SupplierKey> _supplierKeys = Array.Empty<SupplierKey>();

    private QuickModel _quick;
    private IModelKeyBuilder _modelKeyBuilder;
    public StableKeyPoolRepository(IModelKeyBuilder modelKeyBuilder, QuickModel quickModel)
    {
        _modelKeyBuilder = modelKeyBuilder;
        _quick = quickModel;
    }
    
    public async Task Replace(List<SupplierKey> supplierKeys)
    {
        var copiedKeys = supplierKeys.ToArray();
        var quickModel = await _modelKeyBuilder.Build(copiedKeys.ToList());
        _quick.Transfer(quickModel);
        Volatile.Write(ref _supplierKeys, copiedKeys);
    }

    public IReadOnlyList<SupplierKey> SupplierKeys()
    {
        return _supplierKeys;
    }
}
