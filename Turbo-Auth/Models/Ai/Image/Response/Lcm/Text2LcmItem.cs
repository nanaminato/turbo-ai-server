using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Response.Lcm;

public class Text2LcmItem
{
    [JsonProperty("task")]
    public dynamic? Task
    {
        get;
        set;
    }
    [JsonProperty("image_file")]
    public string? ImageFile
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