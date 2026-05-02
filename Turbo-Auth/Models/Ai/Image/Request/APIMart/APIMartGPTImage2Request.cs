using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Request.APIMart;

public class APIMartGPTImage2Request
{
    [Required]
    public string Model
    {
        get;
        set;
    } = "gpt-image-2";
    [Required]
    public string Prompt
    {
        get;
        set;
    }

    public int N
    {
        get;
        set;
    } = 1;

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

    [JsonProperty("image_urls")]
    public List<string> ImageUrls
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