using Newtonsoft.Json;
using Turbo_Auth.Models.Ai.Image.Response.Task;
using Turbo_Auth.Models.Ai.Media;

namespace Turbo_Auth.Models.Ai.Image.Response.Lcm;

public class Text2LcmResponse
{
    [JsonProperty("task")]
    public AsyncGenerateTask? Task
    {
        get;
        set;
    }
    [JsonProperty("images")]
    public Text2LcmItem[]? Images
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