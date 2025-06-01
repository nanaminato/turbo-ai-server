using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image;

public class Novita
{
    [JsonProperty("model_name")]
    public string? ModelName
    {
        get;
        set;
    }
}