using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using RelaxKon.Auth.Contracts.Media;

namespace RelaxKon.Auth.Application.Media;

public sealed class ImageRequestException(string field) : Exception("Invalid image generation option")
{
    public string Field { get; } = field;
}

/// <summary>Build only parameters supported by the selected image family; never rewrite the routed model alias.</summary>
public static class ImageGenerationPolicy
{
    private enum Family { Gpt25, Gpt26 }
    private static Family? Known(string model)
    {
        if (Regex.IsMatch(model, "^gpt-image-2[.]6-(sunburst|flare)(-[0-9]{4}-[0-9]{2}-[0-9]{2})?$")) return Family.Gpt26;
        if (Regex.IsMatch(model, "^gpt-image-2[.]5-(sunburst|flare)(-[0-9]{4}-[0-9]{2}-[0-9]{2})?$")) return Family.Gpt25;
        return null;
    }
    public static void RejectObsoleteModel(string model)
    {
        Require(!model.StartsWith("dall-e", StringComparison.OrdinalIgnoreCase) &&
            !Regex.IsMatch(model, "^gpt-image-(1|1[.]5|1-mini|2)(-[0-9]{4}-[0-9]{2}-[0-9]{2})?$", RegexOptions.IgnoreCase), "model");
    }
    private static void Require([DoesNotReturnIf(false)] bool condition, string field) { if (!condition) throw new ImageRequestException(field); }
    public static Dictionary<string, object> Build(ImageGenerationRequest request, string upstreamModel, bool dalle)
    {
        RejectObsoleteModel(request.Model); RejectObsoleteModel(upstreamModel);
        var family = Known(upstreamModel) ?? Known(request.Model);
        Require(family is not null, "model");
        Require(!string.IsNullOrWhiteSpace(request.Prompt) && request.Prompt.Length <= 32000, "prompt");
        Require(request.N is >= 1 and <= 10, "n");
        var output = request.OutputFormat ?? "png";
        Require(new[] {"png","jpeg","webp","gif","svg"}.Contains(output), "output_format");
        Require(output != "svg" || request.N == 1, "n");
        var size = request.Size;
        Require(size is not null && Regex.IsMatch(size, "^(auto|[1-9][0-9]{0,4}x[1-9][0-9]{0,4})$"), "size");
        if (size != "auto")
        {
            var parts = size.Split('x');
            Require(parts.Length == 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _), "size");
            var width = int.Parse(parts[0]); var height = int.Parse(parts[1]);
            Require(width > 0 && height > 0 && width <= 3840 && height <= 3840 && width % 16 == 0 && height % 16 == 0 &&
                (long)width * height <= 8294400 && (output == "gif" || (long)width * height >= 655360) &&
                width <= 3L * height && height <= 3L * width, "size");
        }
        var payload = new Dictionary<string, object> { ["model"] = upstreamModel, ["prompt"] = request.Prompt,
            ["n"] = request.N, ["size"] = size };
        void Add(string key, object? value) { if (value is not null) payload[key] = value; }
        var quality = request.Quality;
        Require(quality is null || new[] {"auto","low","medium","high","xhigh","max"}.Contains(quality), "quality");
        Require(request.Background is null or "auto" or "opaque" or "transparent", "background");
        Require(request.Background != "transparent" || output is "png" or "webp", "background");
        Require(request.Moderation is null or "auto" or "low", "moderation");
        Add("quality", quality); Add("background", request.Background); Add("moderation", request.Moderation); Add("output_format", output);
        if (output is "jpeg" or "webp")
        {
            Require(request.OutputCompression is null or >= 0 and <= 100, "output_compression");
            Add("output_compression", request.OutputCompression);
        }
        if (family == Family.Gpt26)
        {
            Require(request.Complexity is null or "low" or "medium" or "high", "complexity");
            Add("complexity", request.Complexity);
        }
        if (output == "gif")
        {
            // Conservative common subset: the API reference currently caps frames at16, while the guide allows64.
            Require(request.FrameCount is null or >= 2 and <= 16, "frame_count");
            Require(request.FrameDurationMs is null or >= 20 and <= 2000, "frame_duration_ms");
            Add("frame_count", request.FrameCount); Add("frame_duration_ms", request.FrameDurationMs);
        }
        return payload;
    }

    public static ImageGenerationResponse Response(BinaryData content, IReadOnlyDictionary<string, object> request)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Invalid image provider response");
        static string? Text(JsonElement element, string key) => element.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;
        static long Number(JsonElement element, string key) => element.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var value) && value >= 0 ? value : 0;
        static string? SafeUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" && string.IsNullOrEmpty(uri.UserInfo) ? value : null;
        var images = root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object)
                .Select(item => new ImageResultResponse(SafeUrl(Text(item,"url")), Text(item,"b64_json"), Text(item,"revised_prompt"))).ToList() : [];
        images.RemoveAll(image => string.IsNullOrEmpty(image.Url) && string.IsNullOrEmpty(image.Base64));
        if (images.Count == 0) throw new InvalidDataException("Image provider response contains no images");
        var format = Text(root,"output_format");
        if (format is not ("png" or "jpeg" or "webp" or "gif" or "svg")) format = request.GetValueOrDefault("output_format") as string;
        ImageUsageResponse? usage = root.TryGetProperty("usage", out var value) && value.ValueKind == JsonValueKind.Object
            ? new(Number(value,"input_tokens"), Number(value,"output_tokens"), Number(value,"total_tokens")) : null;
        return new(Number(root,"created"), images, Text(root,"background"), format, Text(root,"quality"), Text(root,"size"), usage);
    }
}
