using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Request.GPTImage;

public class GPTImageCreateRequest
{
    
    [Required]
    [JsonProperty("prompt")]
    public string? Prompt
    {
        get;
        set;
    }

    [JsonProperty("background")]
    public string? Background
    {
        get;
        set;
    }
    [JsonProperty("model")]
    public string? Model
    {
        get;
        set;
    } = "dall-e-3";

    [JsonProperty("moderation")]
    public string? Moderation
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

    [JsonProperty("output_format")]
    public string? OutputFormat
    {
        get;
        set;
    }

    

    [JsonProperty("quality")]
    public string? Quality
    {
        get;
        set;
    } = "standard";

    [JsonProperty("response_format")]
    public string? ResponseFormat
    {
        get;
        set;
    } = "url";
    [JsonProperty("size")]
    public string? Size
    {
        get;
        set;
    } = "1024x1024";

    [JsonProperty("style")]
    public string? Style
    {
        get;
        set;
    } = "vivid";
}