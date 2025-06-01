using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Response.Lcm;

public class Image2LcmInfo
{
    [JsonProperty("image_url")]
    public string? ImageUrl
    {
        get;
        set;
    }

    [JsonProperty("image expire time")]
    public int ImageExpireTime
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