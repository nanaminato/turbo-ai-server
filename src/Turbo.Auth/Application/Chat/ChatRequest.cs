namespace Turbo.Auth.Application.Chat;

// Application input is independent of HTTP binding, JSON libraries and provider SDKs.
public sealed class ChatRequest
{
    public int? AccountId { get; init; }
    public string? ConversationId { get; init; }
    public Message[] Messages { get; init; } = [];
    public string? Model { get; init; }
    public bool Stream { get; init; }
    public bool Vision { get; init; }
}
public sealed class Message
{
    public string Role { get; init; } = "user";
    public ChatContent Content { get; init; } = ChatContent.Empty;
}
public sealed record ChatContent(string? Text, IReadOnlyList<ChatContentPart> Parts)
{
    public static readonly ChatContent Empty = new("", []);
    public static implicit operator ChatContent(string text) => new(text, []);
    public string PlainText => Text ?? string.Join("\n", Parts.Where(part => part.Text is not null).Select(part => part.Text));
}
public sealed record ChatContentPart(string? Text = null, string? ImageUrl = null);
