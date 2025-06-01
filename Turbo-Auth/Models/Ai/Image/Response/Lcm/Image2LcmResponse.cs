using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Response.Lcm;

public class Image2LcmResponse
{
    [JsonProperty("images")]
    public Image2LcmInfo[]? Images
    {
        get;
        set;
    }

    [JsonProperty("image")]
    public Image2LcmItem[]? Image
    {
        get;
        set;
    }

    [JsonProperty("code")]
    public string? Code
    {
        get;
        set;
    }
    [JsonProperty("reason")]
    public string? Reason
    {
        get;
        set;
    }
    [JsonProperty("message")]
    public string? Message
    {
        get;
        set;
    }
    [JsonProperty("metadata")]
    public MetaData? MetaData
    {
        get;
        set;
    }
}