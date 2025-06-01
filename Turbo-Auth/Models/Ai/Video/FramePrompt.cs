using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Video;

public class FramePrompt
{
    [Required]
    [JsonProperty("frames")]
    public int Frames
    {
        get;
        set;
    }
    [StringLength(1024)]
    [Required]
    [JsonProperty("prompt")]
    public string? Prompt
    {
        get;
        set;
    }
}