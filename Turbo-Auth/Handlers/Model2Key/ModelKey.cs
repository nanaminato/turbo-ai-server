using Turbo_Auth.Models.Suppliers;

namespace Turbo_Auth.Handlers.Model2Key;

public class ModelKey
{
    public string? Model
    {
        get;
        set;
    }

    public SupplierKey? SupplierKey
    {
        get;
        set;
    }

    public override string ToString()
    {
        return $"model: {Model}; apiKey: {SupplierKey!.ApiKey}; baseUrl: {SupplierKey!.BaseUrl};\n";
    }
}

public class ModelWeight
{
    public string? Model
    {
        get;
        set;
    }

    public WeightKey? WeightKey
    {
        get;
        set;
    }
}