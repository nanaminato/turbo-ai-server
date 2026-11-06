using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
namespace RelaxKon.Auth.Contracts.Media;

public class AudioTranslationRequest
{
    [Required, Base64String] public string File { get; init; } = "";
    [Required, StringLength(200)] public string Model { get; init; } = "";
    public string? Prompt { get; init; }
    [Range(0, 1)] public float? Temperature { get; init; }
    [JsonProperty("response_format"), AllowedValues(null, "json", "text", "srt", "verbose_json", "vtt")]
    public string? ResponseFormat { get; init; }
    [Required, RegularExpression("^[a-zA-Z0-9]{1,12}$")] public string Suffix { get; init; } = "mp3";
}
