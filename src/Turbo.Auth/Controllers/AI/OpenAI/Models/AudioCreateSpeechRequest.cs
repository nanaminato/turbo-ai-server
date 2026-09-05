using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Turbo.Auth.Controllers.AI.OpenAI.Models;

/// <summary>
/// 文本转语音（TTS）请求体。独立于 SDK 的 DTO，用于控制器入参绑定。
/// </summary>
public class AudioCreateSpeechRequest
{
    [Required]
    [JsonProperty("model")]
    public string? Model
    {
        get;
        set;
    } = "tts-1";

    [Required]
    [JsonProperty("input")]
    public string? Input
    {
        get;
        set;
    }

    [Required]
    [JsonProperty("voice")]
    public string? Voice
    {
        get;
        set;
    } = "alloy";

    [JsonProperty("response_format")]
    public string? ResponseFormat
    {
        get;
        set;
    } = "mp3";

    [JsonProperty("speed")]
    public double? Speed
    {
        get;
        set;
    }
}