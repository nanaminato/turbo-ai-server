using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Turbo.Auth.Controllers.AI.APIMart.Models;

public class APIMartGPTImage2Request
{
    [Required]
    [JsonProperty("model")]
    public string Model
    {
        get;
        set;
    } = "gpt-image-2";
    [Required]
    [JsonProperty("prompt")]
    public string Prompt
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
    } = "1:1";
    [JsonProperty("resolution")]
    public string? Resolution
    {
        get;
        set;
    } = "1k";

    [JsonProperty("image_urls")]
    public List<string>? ImageUrls
    {
        get;
        set;
    } = [];
    [JsonProperty("official_fallback")]
    public bool OfficialFallBack
    {
        get;
        set;
    } = false;
}