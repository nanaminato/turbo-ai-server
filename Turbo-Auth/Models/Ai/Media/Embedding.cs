using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Media;

public class Embedding
{
    [JsonProperty("model_name")]
    public string? ModelName
    {
        get;
        set;
    }
}