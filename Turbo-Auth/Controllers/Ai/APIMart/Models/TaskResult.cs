using Newtonsoft.Json;

namespace Turbo_Auth.Controllers.Ai.APIMart.Models;

public class TaskResult
{
    [JsonProperty("images")]
    public List<ImageItem> Images { get; set; }
}