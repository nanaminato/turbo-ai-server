using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Media;

public class AsyncGenerateTask
{
    [JsonProperty("task_id")]
    public string? TaskId
    {
        get;
        set;
    }
}