using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Response.Task;

public class TaskFinalImage
{
    [JsonProperty("image_url")]
    public string? ImageUrl
    {
        get;
        set;
    }

    [JsonProperty("image_url_ttl")]
    public int ImageUrlTtl
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

    [JsonProperty("nsfw_detection_result")]
    public dynamic? NsfwDetectionResult
    {
        get;
        set;
    }
}