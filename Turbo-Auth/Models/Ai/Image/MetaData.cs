using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image;

public class MetaData
{
    [JsonProperty("task_id")]
    public string? TaskId
    {
        get;
        set;
    }
}