using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
namespace Turbo.Auth.Contracts.Media;
public sealed class APIMartImageGenerationRequest
{
    [Required,StringLength(100)] public string Model { get; init; } = "gpt-image-2.6-flare";
    [Required,StringLength(32000)] public string Prompt { get; init; } = "";
    public string? Version { get; init; }
    public string? Size { get; init; }
    public string? Resolution { get; init; }
    [JsonConverter(typeof(APIMartIntegerConverter)),Range(1,4)] public int N { get; init; } = 1;
    public string? Quality { get; init; }
    [JsonProperty("output_format")] public string? OutputFormat { get; init; }
    [JsonConverter(typeof(APIMartIntegerConverter)),JsonProperty("output_compression"),Range(0,100)] public int? OutputCompression { get; init; }
    public string? Background { get; init; }
    public string? Moderation { get; init; }
    [JsonProperty("image_urls"),MaxLength(16)] public IReadOnlyList<string>? ImageUrls { get; init; }
    [JsonProperty("mask_url")] public string? MaskUrl { get; init; }
    [JsonProperty("markup_url")] public string? MarkupUrl { get; init; }
    public string? Complexity { get; init; }
    [JsonConverter(typeof(APIMartIntegerConverter)),JsonProperty("frame_count"),Range(2,16)] public int? FrameCount { get; init; }
    [JsonConverter(typeof(APIMartIntegerConverter)),JsonProperty("frame_duration_ms"),Range(20,655350)] public int? FrameDurationMs { get; init; }
    public APIMartSvgOptions? Svg { get; init; }
    [JsonProperty("idempotency_key"),StringLength(128)] public string? IdempotencyKey { get; init; }
}
public sealed class APIMartSvgOptions
{
    [Required,StringLength(200)] public string Model { get; init; } = "";
    public APIMartSvgReasoning? Reasoning { get; init; }
}
public sealed class APIMartSvgReasoning { public string? Effort { get; init; } }
public sealed class APIMartReceiptRecoveryRequest
{
    [Required,StringLength(12000),JsonProperty("recovery_token")] public string RecoveryToken { get; init; } = "";
}

// APIMart requires JSON integer tokens; Newtonsoft otherwise coerces fractional/string values into int.
public sealed class APIMartIntegerConverter : JsonConverter
{
    public override bool CanWrite=>false;
    public override bool CanConvert(Type objectType)=>objectType==typeof(int)||objectType==typeof(int?);
    public override object? ReadJson(JsonReader reader,Type objectType,object? existingValue,JsonSerializer serializer)
    {
        if(reader.TokenType==JsonToken.Null&&objectType==typeof(int?))return null;
        if(reader.TokenType!=JsonToken.Integer)throw new JsonSerializationException("Expected a JSON integer");
        try{return Convert.ToInt32(reader.Value,System.Globalization.CultureInfo.InvariantCulture);}
        catch(Exception error)when(error is OverflowException or InvalidCastException or FormatException){throw new JsonSerializationException("Integer out of range");}
    }
    public override void WriteJson(JsonWriter writer,object? value,JsonSerializer serializer)=>throw new NotSupportedException();
}
