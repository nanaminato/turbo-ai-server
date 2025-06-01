using System.ComponentModel.DataAnnotations;

namespace Turbo_Auth.Models.Ai.Media;

public class NovitaModel
{
    [Key]
    public int ModelId
    {
        get;
        set;
    }
    public string? Model
    {
        get;
        set;
    }

    public string? Cover
    {
        get;
        set;
    }

    public string? Type
    {
        get;
        set;
    }

    public bool Nsfw
    {
        get;
        set;
    }

    public bool Sdxl
    {
        get;
        set;
    }
}