using System.ComponentModel.DataAnnotations;
using iTextSharp.text;
using Newtonsoft.Json;
using Turbo_Auth.Models.Ai.Image;
using Turbo_Auth.Models.Ai.Media;

namespace Turbo_Auth.Models.Ai.Video.Request;

public class Text2Video
{
    [JsonProperty("extra")]
    public dynamic? Extra
    {
        get;
        set;
    }
    [JsonProperty("model_name")]
    [Required]
    public string? ModelName
    {
        get;
        set;
    }
    [Required]
    [Range(256,1024)]
    [JsonProperty("height")]
    public int Height
    {
        get;
        set;
    }
    [Required]
    [Range(256,1024)]
    [JsonProperty("width")]
    public int Width
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

    [Required]
    [Range(1, 50)]
    [JsonProperty("steps")]
    public int Steps
    {
        get;
        set;
    } = 20;
    
    [JsonProperty("negative_prompt")]
    public string? NegativePrompt
    {
        get;
        set;
    }
    [Required]
    [Length(1,128)]
    [JsonProperty("prompts")]
    public List<FramePrompt>? Prompts
    {
        get;
        set;
    }

    [Range(1, 30)]
    [JsonProperty("guidance_scale")]
    public float GuidanceScale
    {
        get;
        set;
    } = 7.5f;
    [MaxLength(5)]
    [JsonProperty("lora")]
    public List<Lora>? Loras
    {
        get;
        set;
    }
    [MaxLength(5)]
    [JsonProperty("embeddings")]
    public List<Embedding>? Embeddings
    {
        get;
        set;
    }

    [JsonProperty("closed_loop")]
    public bool ClosedLoop
    {
        get;
        set;
    }

    [JsonProperty("clip_skip")]
    public float ClipSkip
    {
        get;
        set;
    }
    
}