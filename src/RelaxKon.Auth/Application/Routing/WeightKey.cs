using RelaxKon.Auth.Models.Suppliers;

namespace RelaxKon.Auth.Application.Routing;

public class WeightKey
{
    public int RouteId
    {
        get;
        set;
    }

    public int Priority
    {
        get;
        set;
    }

    public string? ProviderModelValue
    {
        get;
        set;
    }

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
        return $"route: {RouteId}, providerModel: {ProviderModelValue}, priority: {Priority}, baseUrl: {SupplierKey?.BaseUrl}, weight: {Weight}";
    }
}
