using Newtonsoft.Json;

namespace Turbo_Auth.Controllers.Ai.APIMart.Models;

public class TaskResponse
{
    [JsonProperty("code")]
    public int Code { get; set; }

    [JsonProperty("data")]
    public TaskData Data { get; set; }
}