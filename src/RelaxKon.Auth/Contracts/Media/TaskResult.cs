using Newtonsoft.Json;

namespace RelaxKon.Auth.Contracts.Media;

public class TaskResult
{
    [JsonProperty("images")]
    public List<ImageItem> Images { get; set; } = [];
}