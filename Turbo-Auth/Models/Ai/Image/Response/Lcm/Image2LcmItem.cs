using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Response.Lcm;

public class Image2LcmItem
{
    [JsonProperty("image_url")]
    public string? ImageUrl
    {
        get;
        set;
    }

    [JsonProperty("image_url_ttl")]
    public string? ImageUrlTtl
    {
        get;
        set;
    }

    [JsonProperty("image_type")]
    public string? ImageType
    {
        get;
        set;
    }
}