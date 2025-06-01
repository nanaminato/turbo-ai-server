using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Media;

public class Lora
{
    [JsonProperty("model_name")]
    public string? ModelName
    {
        get;
        set;
    }

    [Range(-10,10)]
    [JsonProperty("strength")]
    public float Strength
    {
        get;
        set;
    }
}