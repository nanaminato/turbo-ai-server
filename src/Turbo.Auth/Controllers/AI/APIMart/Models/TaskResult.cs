using Newtonsoft.Json;

namespace Turbo.Auth.Controllers.AI.APIMart.Models;

public class TaskResult
{
    [JsonProperty("images")]
    public List<ImageItem> Images { get; set; }
}