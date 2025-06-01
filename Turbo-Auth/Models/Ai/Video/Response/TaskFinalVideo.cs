using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Video.Response;

public class TaskFinalVideo
{
    [JsonProperty("video_url")]
    public string? VideoUrl
    {
        get;
        set;
    }

    [JsonProperty("video_url_ttl")]
    public string? VideoUrlTtl
    {
        get;
        set;
    }

    [JsonProperty("video_type")]
    public string? VideoType
    {
        get;
        set;
    }
}