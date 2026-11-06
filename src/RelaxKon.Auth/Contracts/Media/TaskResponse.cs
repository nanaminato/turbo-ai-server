using Newtonsoft.Json;

namespace RelaxKon.Auth.Contracts.Media;

public class TaskResponse
{
    [JsonProperty("code")]
    public int Code { get; set; }

    [JsonProperty("data")]
    public TaskData? Data { get; set; }
}