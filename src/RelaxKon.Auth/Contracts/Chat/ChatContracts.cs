using RelaxKon.Auth.Contracts.Common;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RelaxKon.Auth.Application.Chat;

namespace RelaxKon.Auth.Contracts.Chat;

public sealed class ChatCompletionRequest
{
    [Required, StringLength(200)] public string Model { get; init; } = "";
    [Required, MinLength(1), NonNullItems] public ChatMessageRequest[] Messages { get; init; } = [];
    [StringLength(128), JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string? ConversationId { get; init; }
    public bool Stream { get; init; } = true;
    public bool Vision { get; init; }
    public ChatRequest ToApplication(int? accountId = null) => new()
    {
        AccountId = accountId,
        ConversationId = ConversationId,
        Model = Model,
        Stream = Stream,
        Vision = Vision,
        Messages = Messages.Select(message => new Message { Role = message.Role.ToLowerInvariant(), Content = message.Content! }).ToArray()
    };
}
public sealed class ChatMessageRequest : IValidatableObject
{
    [Required, AllowedValues("system", "developer", "user", "assistant")] public string Role { get; init; } = "user";
    [Required, JsonConverter(typeof(ChatContentConverter))] public ChatContent? Content { get; init; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Content is { Text: null, Parts.Count: 0 }) yield return new("Message content must not be an empty array.", [nameof(Content)]);
        foreach (var part in Content?.Parts ?? [])
        {
            if (part.ImageUrl is not { } url) continue;
            if (url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) && url.Contains(";base64,", StringComparison.Ordinal)) continue;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                yield return new("Images require an HTTP(S) URL or base64 image data URL.", [nameof(Content)]);
        }
    }
}
public sealed record ChatModelResponse(string? ModelName, string? ModelValue, bool Vision);
public sealed record ChatStreamEventResponse(string Type, string? Text = null, string? Code = null);

/// <summary>Accepts the existing string/vision array wire format without dynamic runtime binding.</summary>
public sealed class ChatContentConverter : JsonConverter<ChatContent>
{
    public override ChatContent? ReadJson(JsonReader reader, Type type, ChatContent? value, bool hasValue, JsonSerializer serializer)
    {
        var token = JToken.Load(reader);
        if (token.Type == JTokenType.Null) return null;
        if (token.Type == JTokenType.String) return new(token.Value<string>(), []);
        if (token is not JArray array) throw new JsonSerializationException("Content must be text or a content-part array.");
        return new(null, array.Select(ReadPart).ToArray());
    }
    private static ChatContentPart ReadPart(JToken token)
    {
        if (token is not JObject part || part["type"]?.Type != JTokenType.String)
            throw new JsonSerializationException("Content parts require a string type.");
        return part["type"]!.Value<string>() switch
        {
            "text" when part["text"]?.Type == JTokenType.String => new ChatContentPart(Text: part["text"]!.Value<string>()),
            "image_url" when (part["image_url"] as JObject)?["url"]?.Type == JTokenType.String => new ChatContentPart(ImageUrl: part["image_url"]!["url"]!.Value<string>()),
            _ => throw new JsonSerializationException("Content parts require text or image_url.url.")
        };
    }
    public override void WriteJson(JsonWriter writer, ChatContent? content, JsonSerializer serializer)
    {
        if (content is null) { writer.WriteNull(); return; }
        if (content.Text is not null) { writer.WriteValue(content.Text); return; }
        writer.WriteStartArray();
        foreach (var part in content.Parts)
        {
            if (part.Text is not null) serializer.Serialize(writer, new { type = "text", text = part.Text });
            else serializer.Serialize(writer, new { type = "image_url", image_url = new { url = part.ImageUrl } });
        }
        writer.WriteEndArray();
    }
}
