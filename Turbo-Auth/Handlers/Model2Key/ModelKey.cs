using Turbo_Auth.Models.Suppliers;

namespace Turbo_Auth.Handlers.Model2Key;

public class ModelKey
{
    public int RouteId
    {
        get;
        set;
    }

    public string? LogicalModel
    {
        get;
        set;
    }

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
        return $"logicalModel: {LogicalModel}; providerModel: {Model}; route: {RouteId}; baseUrl: {SupplierKey?.BaseUrl};";
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
