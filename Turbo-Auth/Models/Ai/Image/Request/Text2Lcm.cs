using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Turbo_Auth.Models.Ai.Media;

namespace Turbo_Auth.Models.Ai.Image.Request;

public class Text2Lcm
{
    [JsonProperty("model_name")]
    public string? ModelName
    {
        get;
        set;
    }
    [Required]
    [JsonProperty("prompt")]
    public string? Prompt
    {
        get;
        set;
    }

    [JsonProperty("negative_prompt")]
    public string? NegativePrompt
    {
        get;
        set;
    }
    [Required]
    [JsonProperty("height")]
    [Range(128,1024)]
    public int Height
    {
        get;
        set;
    }
    [Required]
    [JsonProperty("width")]
    [Range(128,1024)]
    public int Width
    {
        get;
        set;
    }
    [MaxLength(5)]
    [JsonProperty("loras")]
    [Required]
    public Lora[]? Loras
    {
        get;
        set;
    }
    [JsonProperty("embeddings")]
    [Required]
    [MaxLength(5)]
    public Embedding[]? Embeddings
    {
        get;
        set;
    }

    [Required]
    [Range(1, 16)]
    [JsonProperty("image_num")]
    public int ImageNum
    {
        get;
        set;
    }
    
    [Required]
    [JsonProperty("steps")]
    [Range(1, 8)]
    public int Steps
    {
        get;
        set;
    }
    [JsonProperty("seed")]
    [Required]
    public int Seed
    {
        get;
        set;
    }
    [Required]
    [Range(2.0,14.0)]
    [JsonProperty("guidance_scale")]
    public double GuidanceScale
    {
        get;
        set;
    }

    [Range(1, 12)]
    [JsonProperty("clip_skip")]
    public int ClipSkip
    {
        get;
        set;
    }
    
}