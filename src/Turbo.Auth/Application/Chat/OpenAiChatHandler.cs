#pragma warning disable OPENAI001 // Developer role is part of the supported wire contract.
using System.Runtime.CompilerServices;
using System.ClientModel.Primitives;
using System.Text.Json;
using OpenAI.Chat;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;

namespace Turbo.Auth.Application.Chat;

public sealed class OpenAiChatHandler(OpenAiClientFactory clients, ILogger<OpenAiChatHandler>? logger = null) : IChatHandler
{
    public HandlerType ProviderType => HandlerType.Openai;
    public IAsyncEnumerable<string> Stream(ChatRequest request, ModelKey route, CancellationToken token) => StreamAt(request, route, null, token);
    internal async IAsyncEnumerable<string> StreamAt(ChatRequest request, ModelKey route, string? endpoint, [EnumeratorCancellation] CancellationToken token)
    {
        using var lease = clients.Create(route, endpoint: endpoint);
        var client = lease.Client.GetChatClient(route.Model!);
        var finished = false;
        ChatTokenUsage? usage = null;
        await foreach (var update in client.CompleteChatStreamingAsync(request.Messages.Select(message => Map(message, request.Vision)), cancellationToken: token))
        {
            finished |= update.FinishReason.HasValue;
            if (update.Usage is not null) usage = update.Usage;
            foreach (var part in update.ContentUpdate) if (!string.IsNullOrEmpty(part.Text)) yield return part.Text;
            if (!string.IsNullOrEmpty(update.RefusalUpdate)) yield return update.RefusalUpdate;
        }
        if (!finished) throw new EndOfStreamException("OpenAI stream ended without a finish reason.");
        if (usage is not null && logger is not null)
        {
            // Preserve optional and newer usage fields without logging request/response content.
            using var json = JsonDocument.Parse(ModelReaderWriter.Write(usage).ToString());
            var root = json.RootElement;
            var details = root.TryGetProperty("prompt_tokens_details", out var input) ? input : default;
            logger.LogInformation("AI chat usage. RouteId: {RouteId}; SupplierKeyId: {SupplierKeyId}; LogicalModel: {LogicalModel}; ProviderModel: {ProviderModel}; InputTokens: {InputTokens}; OutputTokens: {OutputTokens}; CachedInputTokens: {CachedInputTokens}; CacheWriteTokens: {CacheWriteTokens}",
                route.RouteId, route.SupplierKey?.SupplierKeyId, route.LogicalModel, route.Model,
                usage.InputTokenCount, usage.OutputTokenCount, Count(details, "cached_tokens"),
                Count(details, "cache_write_tokens") ?? Count(root, "cache_write_tokens"));
        }
    }
    private static long? Count(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var count) && count.TryGetInt64(out var number) ? number : null;
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
