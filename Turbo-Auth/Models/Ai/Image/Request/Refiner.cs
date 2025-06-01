using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Request;

public class Refiner
{
    [JsonProperty("switch_at")]
    [Range(0,1)]
    public float SwitchAt
    {
        get;
        set;
    }
}