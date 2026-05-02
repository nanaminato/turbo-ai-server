using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Response.APIMartTask;

public class TaskData
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("progress")]
    public int Progress { get; set; }

    [JsonProperty("result")]
    public TaskResult Result { get; set; }

    [JsonProperty("created")]
    public long Created { get; set; } // Unix 时间戳

    [JsonProperty("completed")]
    public long Completed { get; set; } // Unix 时间戳

    [JsonProperty("estimated_time")]
    public int EstimatedTime { get; set; }

    [JsonProperty("actual_time")]
    public int ActualTime { get; set; }
}