using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Request;

public class DallE3Request
{
    [JsonProperty("model")]
    public string? Model
    {
        get;
        set;
    } = "dall-e-3";
    [Required]
    [JsonProperty("prompt")]
    public string? Prompt
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

    [JsonProperty("size")]
    public string? Size
    {
        get;
        set;
    } = "1024x1024";

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

    [JsonProperty("style")]
    public string? Style
    {
        get;
        set;
    } = "vivid";
}