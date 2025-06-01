using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Turbo_Auth.Models.Ai.Media;

namespace Turbo_Auth.Models.Ai.Image.Request;

public class Image2Lcm
{
    [JsonProperty("model_name")]
    [Required]
    public string? ModelName
    {
        get;
        set;
    }

    [JsonProperty("input_image")]
    [Required]
    public string? InputImage
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

    [Required]
    [JsonProperty("negative_prompt")]
    public string? NegativePrompt
    {
        get;
        set;
    }

    [Required]
    [JsonProperty("image_num")]
    [Range(1, 16)]
    public int ImageNum
    {
        get;
        set;
    }

    [Required]
    [JsonProperty("sd_vae")]
    public string? SdVae
    {
        get;
        set;
    }
    [MaxLength(5)]
    [JsonProperty("loras")]
    public Lora[]? Loras
    {
        get;
        set;
    }
    [JsonProperty("embeddings")]
    [MaxLength(5)]
    public Embedding[]? Embeddings
    {
        get;
        set;
    }
    [JsonProperty("steps")]
    [Range(1, 8)]
    public int Steps
    {
        get;
        set;
    }
    [Range(2.0,14.0)]
    [JsonProperty("guidance_scale")]
    public double GuidanceScale
    {
        get;
        set;
    }
    [Required]
    [JsonProperty("seed")]
    public int Seed
    {
        get;
        set;
    }
    [Range(1, 12)]
    [JsonProperty("clip_skip")]
    public int? ClipSkip
    {
        get;
        set;
    }

    [Range(0, 1)]
    [JsonProperty("strength")]
    public float? Strength
    {
        get;
        set;
    }
}