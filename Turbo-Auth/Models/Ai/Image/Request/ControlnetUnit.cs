using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Request;

public class ControlnetUnit
{
    [JsonProperty("model_name")]
    public string? ModelName
    {
        get;
        set;
    }
}