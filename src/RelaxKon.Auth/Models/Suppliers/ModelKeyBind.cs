using System.ComponentModel.DataAnnotations;

namespace RelaxKon.Auth.Models.Suppliers;

public class ModelKeyBind
{
    [Key]
    public int ModelKeyBindId
    {
        get;
        set;
    }

    public bool Enable
    {
        get;
        set;
    }

    [Required]
    public int SupplierKeyId
    {
        get;
        set;
    }
    public SupplierKey? SupplierKey { get; set; }
    [Required]
    public int ModelId
    {
        get;
        set;
    }

    public Model? Model
    {
        get;
        set;
    }
    [Required]
    public double Fee
    {
        get;
        set;
    }

    // Null preserves legacy inverse-fee selection. Explicit zero excludes this route.
    [Range(0d, 1000000d)]
    public double? RoutingWeight { get; set; }

    [MaxLength(200)]
    public string ProviderModelValue
    {
        get;
        set;
    } = string.Empty;

    public int Priority
    {
        get;
        set;
    }

    public override string ToString()
    {
        return $"ModelKeyBindId: {ModelKeyBindId}, Enable: {Enable}, SupplierKeyId: {SupplierKeyId}, ModelId: {ModelId}, ProviderModelValue: {ProviderModelValue}, Priority: {Priority}, Fee: {Fee}";
    }
}
