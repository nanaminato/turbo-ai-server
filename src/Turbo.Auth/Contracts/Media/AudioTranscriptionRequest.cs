using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
namespace Turbo.Auth.Contracts.Media;

public sealed class AudioTranscriptionRequest
{
    [Required, Base64String] public string File { get; init; } = "";
    [Required, StringLength(200)] public string Model { get; init; } = "";
    public string? Prompt { get; init; }
    [StringLength(16)] public string? Language { get; init; }
    [Range(0, 1)] public float? Temperature { get; init; }
    [JsonProperty("response_format"), AllowedValues(null, "json", "text", "srt", "verbose_json", "vtt", "diarized_json")]
    public string? ResponseFormat { get; init; }
    [Required, RegularExpression("^[a-zA-Z0-9]{1,12}$")] public string Suffix { get; init; } = "mp3";
}
