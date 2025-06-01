using Turbo_Auth.Models.Suppliers;

namespace Turbo_Auth.Handlers.Model2Key;

public class WeightKey
{
    public double Weight
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
        var builder = $"key: {SupplierKey?.ApiKey}, baseUrl: {SupplierKey?.BaseUrl}, Weight: {Weight}";
        return builder;
    }
}