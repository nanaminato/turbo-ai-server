using System.Runtime.CompilerServices;
using Anthropic;
using Anthropic.Models.Messages;
using RelaxKon.Auth.Application.Providers;
using RelaxKon.Auth.Application.Routing;

namespace RelaxKon.Auth.Application.Chat;

public sealed class AnthropicChatHandler(IHttpClientFactory clients) : IChatHandler
{
    public HandlerType ProviderType => HandlerType.Anthropic;
    public async IAsyncEnumerable<string> Stream(ChatRequest request, ModelKey route, [EnumeratorCancellation] CancellationToken token)
    {
        using var http = clients.CreateClient("AiProvider");
        using var client = new AnthropicClient(new Anthropic.Core.ClientOptions
        {
            HttpClient = http,
            ApiKey = route.SupplierKey!.ApiKey,
            BaseUrl = string.IsNullOrWhiteSpace(route.SupplierKey.BaseUrl) ? "https://api.anthropic.com" : route.SupplierKey.BaseUrl.TrimEnd('/'),
            Timeout = TimeSpan.FromMinutes(5),
            MaxRetries = 0
        });
        var system = string.Join("\n", request.Messages.Where(message => message.Role is "system" or "developer").Select(message => message.Content.PlainText));
        var parameters = new MessageCreateParams
        {
            MaxTokens = 8192,
            Model = route.Model!,
            System = string.IsNullOrEmpty(system) ? null : new MessageCreateParamsSystem(system),
            Messages = request.Messages.Where(message => message.Role is not ("system" or "developer")).Select(message =>
                message.Role == "assistant" ? new MessageParam(new AssistantMessageParam { Content = Content(message.Content, request.Vision) })
                    : new MessageParam(new UserMessageParam { Content = Content(message.Content, request.Vision) })).ToList()
        };
        var finished = false;
        await foreach (var update in client.Messages.CreateStreaming(parameters, token))
        {
            finished |= update.TryPickStop(out _);
            if (update.TryPickContentBlockDelta(out var delta) && delta.Delta.TryPickText(out var text)) yield return text.Text;
        }
        if (!finished) throw new EndOfStreamException("Anthropic stream ended without a stop event.");
    }
    private static MessageContentParam Content(ChatContent content, bool vision)
    {
        if (content.Text is not null) return content.Text;
        var parts = new List<ContentBlockParam>();
        foreach (var part in content.Parts)
        {
            if (part.Text is not null) parts.Add(new TextBlockParam { Text = part.Text });
            else if (vision && part.ImageUrl is { } url)
            {
                if (!url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) parts.Add(new ImageBlockParam { Source = new UrlImageSource { Url = url } });
                else
                {
                    var separator = url.IndexOf(','); parts.Add(new ImageBlockParam
                    {
                        Source = new Base64ImageSource
                        {
                            MediaType = url[5..url.IndexOf(';')],
                            Data = url[(separator + 1)..]
                        }
                    });
                }
            }
        }
        return new MessageContentParam(parts);
    }
}
