#pragma warning disable OPENAI001 // Developer role is part of the supported wire contract.
using System.Runtime.CompilerServices;
using OpenAI.Chat;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;

namespace Turbo.Auth.Application.Chat;

public sealed class OpenAiChatHandler(OpenAiClientFactory clients) : IChatHandler
{
    public HandlerType ProviderType => HandlerType.Openai;
    public IAsyncEnumerable<string> Stream(ChatRequest request, ModelKey route, CancellationToken token) => StreamAt(request, route, null, token);
    internal async IAsyncEnumerable<string> StreamAt(ChatRequest request, ModelKey route, string? endpoint, [EnumeratorCancellation] CancellationToken token)
    {
        using var lease = clients.Create(route, endpoint: endpoint);
        var client = lease.Client.GetChatClient(route.Model!);
        var finished = false;
        await foreach (var update in client.CompleteChatStreamingAsync(request.Messages.Select(message => Map(message, request.Vision)), cancellationToken: token))
        {
            finished |= update.FinishReason.HasValue;
            foreach (var part in update.ContentUpdate) if (!string.IsNullOrEmpty(part.Text)) yield return part.Text;
            if (!string.IsNullOrEmpty(update.RefusalUpdate)) yield return update.RefusalUpdate;
        }
        if (!finished) throw new EndOfStreamException("OpenAI stream ended without a finish reason.");
    }
    private static ChatMessage Map(Message message, bool vision) => message.Role switch
    {
        "system" => new SystemChatMessage(message.Content.PlainText),
        "developer" => new DeveloperChatMessage(message.Content.PlainText),
        "assistant" => new AssistantChatMessage(message.Content.PlainText),
        _ => new UserChatMessage(Parts(message.Content, vision))
    };
    private static IEnumerable<ChatMessageContentPart> Parts(ChatContent content, bool vision)
    {
        if (content.Text is not null) { yield return ChatMessageContentPart.CreateTextPart(content.Text); yield break; }
        foreach (var part in content.Parts)
        {
            if (part.Text is not null) yield return ChatMessageContentPart.CreateTextPart(part.Text);
            else if (vision && part.ImageUrl is not null) yield return ChatMessageContentPart.CreateImagePart(new Uri(part.ImageUrl));
        }
    }
}

#pragma warning restore OPENAI001
