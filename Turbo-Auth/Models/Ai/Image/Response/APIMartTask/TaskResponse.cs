using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Response.APIMartTask;

public class TaskResponse
{
    [JsonProperty("code")]
    public int Code { get; set; }

    [JsonProperty("data")]
    public TaskData Data { get; set; }
}