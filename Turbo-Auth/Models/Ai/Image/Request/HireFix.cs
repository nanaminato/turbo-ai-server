using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Request;

public class HireFix
{
    [Required]
    [Range(128,4096)]
    [JsonProperty("target_width")]
    public int TargetWidth
    {
        get;
        set;
    }

    [Required]
    [Range(128,4096)]
    [JsonProperty("target_height")]
    public int TargetHeight
    {
        get;
        set;
    }
    [Range(0,1)]
    [JsonProperty("strength")]
    public float Strength
    {
        get;
        set;
    }
    /* 枚举
     * RealESRNet_x4plus RealESRGAN_x4plus_anime_6B
        Latent
     */
    
    [JsonProperty("upscaler")]
    public string? Upscaler
    {
        get;
        set;
    }
}