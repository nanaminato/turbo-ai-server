using System.ComponentModel.DataAnnotations;

namespace Turbo_Auth.Models.Suppliers;

public class AvailableModel
{
    [Key]
    public int ModelId
    {
        get;
        set;
    }

    public bool Enable
    {
        get;
        set;
    }

    public bool IsChatModel
    {
        get;
        set;
    } = true;

    public bool Vision
    {
        get;
        set;
    } = true;
    [Required]
    [MaxLength(200)]
    public string? Name
    {
        get;
        set;
    }
    [Required]
    [MaxLength(200)]
    public string? ModelValue
    {
        get;
        set;
    }
    public override string ToString()
    {
        return $"ModelId: {ModelId}, Name: {Name}";
    }
}