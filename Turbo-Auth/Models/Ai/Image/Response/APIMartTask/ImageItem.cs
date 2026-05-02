using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Response.APIMartTask;

public class ImageItem
{
    [JsonProperty("url")]
    public List<string> Url { get; set; } 

    [JsonProperty("expires_at")]
    public long ExpiresAt { get; set; } // Unix 时间戳
}