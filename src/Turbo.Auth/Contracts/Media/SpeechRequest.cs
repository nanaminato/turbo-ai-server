using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
namespace Turbo.Auth.Contracts.Media;

public sealed class SpeechRequest
{
    [Required, StringLength(200)] public string Model { get; init; } = "tts-1";
    [Required] public string Input { get; init; } = "";
    [Required, StringLength(200)] public string Voice { get; init; } = "alloy";
    [JsonProperty("response_format"), AllowedValues("mp3", "opus", "aac", "flac", "wav", "pcm")]
    public string ResponseFormat { get; init; } = "mp3";
    [Range(0.25, 4.0)] public float? Speed { get; init; }
}
