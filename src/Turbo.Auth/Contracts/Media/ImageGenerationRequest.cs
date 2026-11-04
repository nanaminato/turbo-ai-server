using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
namespace Turbo.Auth.Contracts.Media;

public sealed class ImageGenerationRequest
{
    [Required] public string Prompt { get; init; } = "";
    [Required, StringLength(200)] public string Model { get; init; } = "dall-e-3";
    [Range(1, 10), JsonProperty("n")] public int N { get; init; } = 1;
    [AllowedValues(null, "transparent", "opaque", "auto")] public string? Background { get; init; }
    [AllowedValues(null, "low", "auto")] public string? Moderation { get; init; }
    [JsonProperty("output_format"), AllowedValues(null, "png", "jpeg", "webp")] public string? OutputFormat { get; init; }
    [AllowedValues(null, "standard", "hd", "low", "medium", "high", "auto")] public string? Quality { get; init; }
    [JsonProperty("response_format"), AllowedValues("url", "b64_json")] public string ResponseFormat { get; init; } = "url";
    [RegularExpression("^(auto|[1-9][0-9]{0,4}x[1-9][0-9]{0,4})$")] public string Size { get; init; } = "1024x1024";
    [AllowedValues(null, "vivid", "natural")] public string? Style { get; init; }
}
