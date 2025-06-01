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
    /*
     * 可以构建一个编译好的实际表，这个表根据所有的key以及价格等构建
     * 对于任意一个模型，支持该模型的若干个key，并且包含一个优先级
     * 这样，当传递一个模型时可以迅速找到一个模型或者替代模型
     * 这还需要在服务端写死一个可替代模型表，或者把可替代模型写在
     * 数据库中，不过前者是一个简便的方式。
     * 这样针对一个模型，我们得到了一个模型和一个请求方式，请求源
     * 然后我们就可以进行响应了。
     * *
     */
}