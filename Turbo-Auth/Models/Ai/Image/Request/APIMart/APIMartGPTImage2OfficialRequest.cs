using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Request.APIMart;

public class APIMartGPTImage2OfficialRequest
{
    [JsonProperty("model")]
    public string? Model
    {
        get;
        set;
    } = "gpt-image-2-official";
    [Required]
    [JsonProperty("prompt")]
    public string? Prompt
    {
        get;
        set;
    }
    public string? Size
    {
        get;
        set;
    } = "1:1";

    public string? Resolution
    {
        get;
        set;
    } = "1k";
    [JsonProperty("quality")]
    public string? Quality
    {
        get;
        set;
    } = "auto";
    [JsonProperty("background")]
    public string? Background
    {
        get;
        set;
    }
   

    [JsonProperty("moderation")]
    public string? Moderation
    {
        get;
        set;
    }
    [JsonProperty("output_format")]
    public string? OutputFormat
    {
        get;
        set;
    }
    [JsonProperty("n")]
    public int N
    {
        get;
        set;
    } = 1;

    [JsonProperty("image_urls")]
    public List<string>? ImageUrls
    {
        get;
        set;
    }

    [JsonProperty("mask_url")]
    public string? MaskUrl
    {
        get;
        set;
    }
}