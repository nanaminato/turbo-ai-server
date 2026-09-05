using System.ComponentModel.DataAnnotations;

namespace Turbo.Auth.Models.Suppliers;

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
