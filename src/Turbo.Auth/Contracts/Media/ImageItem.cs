using Newtonsoft.Json;

namespace Turbo.Auth.Contracts.Media;

public class ImageItem
{
    [JsonProperty("url")]
    public List<string> Url { get; set; } = [];

    [JsonProperty("expires_at")]
    public long ExpiresAt { get; set; } // Unix 时间戳
}