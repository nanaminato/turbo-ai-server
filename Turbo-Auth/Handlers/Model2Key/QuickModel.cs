using Turbo_Auth.Models.Suppliers;

namespace Turbo_Auth.Handlers.Model2Key;

public class QuickModel
{
    private Dictionary<string, List<WeightKey>> _quick = new();
    private List<SupplierKey> _novitaKeys = new();
    private List<SupplierKey> _apiMartKeys = new();

    public SupplierKey GetNovitaKey()
    {
        var rand = new Random();
        return _novitaKeys[rand.Next(_novitaKeys.Count)];
    }

    public SupplierKey? GetApiMartKey()
    {
        if (_apiMartKeys.Count == 0)
        {
            return null;
        }
        var rand = new Random();
        return _apiMartKeys[rand.Next(_apiMartKeys.Count)];
    }

    public List<SupplierKey>? NovitaKeys
    {
        get;
        set;
    }

    public List<SupplierKey>? ApiMartKeys
    {
        get;
        set;
    }

    public Dictionary<string, List<WeightKey>> GetQuick()
    {
        return _quick;
    }
    public List<ModelKey> GetModelKeys(string model)
    {
        var weightKeys = _quick[model];
        var mws = new List<ModelKey>();
        foreach (var weight in weightKeys)
        {
            mws.Add(new ModelKey()
            {
                Model = model,
                SupplierKey = weight.SupplierKey
            });
        }

        return mws;
    }
    public ModelKey? GetModelAndKey(string model)
    {
        // 首先尝试获取一个key，如果获取失败
        // 然后查找可替代表，然后进行 负载调度
        // 如果获取成功，进行负载调度
        // 然后返回对应的modelKey
        if (_quick.ContainsKey(model))
        {
            var weights = _quick[model];
            var mws = new List<ModelWeight>();
            foreach (var weight in weights)
            {
                mws.Add(new ModelWeight()
                {
                    Model = model,
                    WeightKey = weight
                });
            }

            var mw = WeightRandom(mws);
            return new ModelKey()
            {
                Model = mw.Model,
                SupplierKey = mw.WeightKey!.SupplierKey
            };
        }

        if (!_quick.ContainsKey(model))
        {
            throw new Exception("当前数据库不存在支持当前模型的密钥");
        }
            
        var mixModelWeights = new List<ModelWeight>();
        foreach (var weight in _quick[model])
        {
            mixModelWeights.Add(new ModelWeight()
            {
                Model = model,
                WeightKey = weight
            });
        }

        var rModelWeight = WeightRandom(mixModelWeights);
        return new ModelKey()
        {
            Model = rModelWeight.Model,
            SupplierKey = rModelWeight.WeightKey!.SupplierKey
        };
    }

    private static ModelWeight WeightRandom(List<ModelWeight> modelWeights)
    {
        var random = new Random();
        var totalWeight = 0d;
        foreach(var item in modelWeights)
        {
            totalWeight += item.WeightKey!.Weight;
        }

        var randNum = random.NextDouble()*totalWeight;
        foreach (var item in modelWeights)
        {
            if (randNum < item.WeightKey!.Weight)
            {
                return item;
            }

            randNum -= item.WeightKey!.Weight;
        }

        return modelWeights.Last();
    }

    public Dictionary<string, List<WeightKey>> Quick
    {
        get => _quick;
        set => _quick = value;
    }

    public void Transfer(QuickModel quickModel)
    {
        _quick.Clear();
        _novitaKeys.Clear();
        _apiMartKeys.Clear();

        foreach (var (key, value) in quickModel.Quick)
        {
            _quick.Add(key,value);
        }
        if(quickModel.NovitaKeys==null) return;
        foreach (var key in quickModel.NovitaKeys!)
        {
            _novitaKeys.Add(key);
        }

        if (quickModel.ApiMartKeys == null) return;
        foreach (var key in quickModel.ApiMartKeys)
        {
            _apiMartKeys.Add(key);
        }
    }

    public void QuickPrintln()
    {
        foreach (var (key,value) in Quick)
        {
            Console.WriteLine($"model: {key}");
            foreach (var weight in value)
            {
                Console.WriteLine(weight);
            }
        }
    }
}