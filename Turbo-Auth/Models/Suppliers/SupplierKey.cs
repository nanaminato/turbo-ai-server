using System.ComponentModel.DataAnnotations;

namespace Turbo_Auth.Models.Suppliers;

public class SupplierKey
{
    [Key]
    public int SupplierKeyId
    {
        get;
        set;
    }
    [Required]
    [MaxLength(200)]
    public string? BaseUrl
    {
        get;
        set;
    }
    [Required]
    public int RequestIdentifier
    {
        get;
        set;
    } = 0;

    public bool Enable
    {
        get;
        set;
    }
    [Required]
    [MaxLength(200)]
    public string? ApiKey
    {
        get;
        set;
    }

    public ICollection<ModelKeyBind>? ModelKeyBinds
    {
        get;
        set;
    }
    public override string ToString()
    {
        var str =  $"SupplierKeyId: {SupplierKeyId}, Enable: {Enable}, BaseUrl: {BaseUrl}, RequestIdentifier: {RequestIdentifier}, ApiKey: {ApiKey}\n";
        if (ModelKeyBinds != null)
        {
            foreach (var modelKeyBind in ModelKeyBinds)
            {
                str += $"    ${modelKeyBind}\n";
            }
        }

        return str;
    }

}