using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Request;

public class Controlnet
{
    [Required]
    [JsonProperty("units")]
    public List<ControlnetUnit>? Units
    {
        get;
        set;
    }
}