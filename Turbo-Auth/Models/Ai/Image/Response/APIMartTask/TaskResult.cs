using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Response.APIMartTask;

public class TaskResult
{
    [JsonProperty("images")]
    public List<ImageItem> Images { get; set; }
}