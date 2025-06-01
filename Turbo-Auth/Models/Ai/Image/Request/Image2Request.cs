using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Turbo_Auth.Models.Ai.Media;

namespace Turbo_Auth.Models.Ai.Image.Request;

public class Image2Request
{
    [JsonProperty("model_name")]
    public string? ModelName
    {
        get;
        set;
    }

    [Required]
    [JsonProperty("image_base64")]
    public string? ImageBase64
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

    [JsonProperty("sd_vae")]
    public string? SdVae
    {
        get;
        set;
    }

    [JsonProperty("controlnet")]
    public Controlnet? Controlnet
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
    [JsonProperty("height")]
    [Range(128,2048)]
    public int Height
    {
        get;
        set;
    }
    [Required]
    [JsonProperty("width")]
    [Range(128,2048)]
    public int Width
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
    [Range(1, 100)]
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
    [Range(1, 12)]
    [JsonProperty("clip_skip")]
    public int ClipSkip
    {
        get;
        set;
    }
    [Required]
    [Range(1.0,30.0)]
    [JsonProperty("guidance_scale")]
    public double GuidanceScale
    {
        get;
        set;
    }

    [Required]
    [JsonProperty("sampler_name")]
    public string? SamplerName
    {
        get;
        set;
    }

    [Required]
    [Range(0.0,1.0)]
    [JsonProperty("strength")]
    public float? Strength
    {
        get;
        set;
    }
}