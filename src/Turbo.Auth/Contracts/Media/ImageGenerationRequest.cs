using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
namespace Turbo.Auth.Contracts.Media;

public sealed class ImageGenerationRequest
{
    [Required, StringLength(32000)] public string Prompt { get; init; } = "";
    [Required, StringLength(200)] public string Model { get; init; } = "gpt-image-2.6-flare";
    [Range(1, 10), JsonProperty("n")] public int N { get; init; } = 1;
    [AllowedValues(null, "transparent", "opaque", "auto")] public string? Background { get; init; }
    [AllowedValues(null, "low", "auto")] public string? Moderation { get; init; }
    [JsonProperty("output_format"), AllowedValues(null, "png", "jpeg", "webp", "gif", "svg")] public string? OutputFormat { get; init; }
    [AllowedValues(null, "low", "medium", "high", "xhigh", "max", "auto")] public string? Quality { get; init; }
    [Required, RegularExpression("^(auto|[1-9][0-9]{0,4}x[1-9][0-9]{0,4})$")] public string Size { get; init; } = "1024x1024";
    [JsonProperty("output_compression"), Range(0, 100)] public int? OutputCompression { get; init; }
    [AllowedValues(null, "low", "medium", "high")] public string? Complexity { get; init; }
    [JsonProperty("frame_count"), Range(2, 16)] public int? FrameCount { get; init; }
    [JsonProperty("frame_duration_ms"), Range(20, 2000)] public int? FrameDurationMs { get; init; }
}
