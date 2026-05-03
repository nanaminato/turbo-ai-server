using Turbo_Auth.Handlers.Differentiator;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Suppliers;

namespace Turbo_Auth.Handlers.Builder;

public class ModelKeyBuilder: IModelKeyBuilder
{
    public async Task<QuickModel> Build(List<SupplierKey> supplierKeys)
    {
        /*
         * 然后就可以构建模型的映射表了，这个表的基本结构是一个字典
         * 针对任何一个模型，会有一个花费评估数组与其对应
         * 后续处理时，根据根据这个数组调度合适的密钥进行使用
         * *
         */
        var models = new HashSet<string>();
        foreach (var key in
                 supplierKeys
                )
        {
            if (key.ModelKeyBinds != null)
            {
                foreach (var model in key.ModelKeyBinds!.Select(m => m.Model!.ModelValue))
                {
                    models.Add(model!);
                }
            }
        }

        var quick = supplierKeys
            .SelectMany(sk => sk.ModelKeyBinds ?? Enumerable.Empty<ModelKeyBind>())
            .Where(mkb => mkb.Enable && mkb.Model != null && models.Contains(mkb.Model.ModelValue!))
            .Where(mkb => !(mkb.Model!.ModelValue == "gpt-image-2" && mkb.SupplierKey!.BaseUrl!.Contains("apimart")))
            .GroupBy(mkb => mkb.Model!.ModelValue)
            .ToDictionary(
                g => g.Key,
                g => g.Select(mkb => new WeightKey {
                    Weight = mkb.Fee != 0 ? 1 / mkb.Fee : 0,
                    SupplierKey = mkb.SupplierKey
                }).ToList()
            );
        var novitaKeys = supplierKeys.Where(s => s.RequestIdentifier == (int)HandlerType.Novita)
            .ToList();
        var apiMartKeys = supplierKeys.Where(s => s.BaseUrl!.Contains("apimart")).ToList();
        return new()
        {
            Quick = quick,
            NovitaKeys = novitaKeys,
            ApiMartKeys = apiMartKeys
        };
    }

}