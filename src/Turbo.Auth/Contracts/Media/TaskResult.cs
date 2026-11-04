using Newtonsoft.Json;

namespace Turbo.Auth.Contracts.Media;

public class TaskResult
{
    [JsonProperty("images")]
    public List<ImageItem> Images { get; set; } = [];
}