using Turbo_Auth.Handlers.Builder;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Suppliers;

namespace Turbo_Auth.Handlers.keyPool;

public class StableKeyPoolRepository: IKeyPoolRepository
{
    private List<SupplierKey> _supplierKeys
        = [];

    private QuickModel _quick;
    private IModelKeyBuilder _modelKeyBuilder;
    public StableKeyPoolRepository(IModelKeyBuilder modelKeyBuilder, QuickModel quickModel)
    {
        _modelKeyBuilder = modelKeyBuilder;
        _quick = quickModel;
    }
    
    public async Task Replace(List<SupplierKey> supplierKeys)
    {
        _supplierKeys.Clear();
        _supplierKeys.AddRange(supplierKeys);
        var quickModel = await _modelKeyBuilder.Build(_supplierKeys);
        _quick.Transfer(quickModel); // 维持单例，迁移构建的数据
    }

    public List<SupplierKey> SupplierKeys()
    {
        return _supplierKeys;
    }
}