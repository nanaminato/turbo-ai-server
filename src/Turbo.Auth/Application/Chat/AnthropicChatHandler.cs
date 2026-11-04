using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Models.AI;
using Turbo.Auth.Models.AI.Chat;
using AnthropicMessage = Anthropic.SDK.Messaging.Message;

namespace Turbo.Auth.Application.Chat;

public class AnthropicChatHandler: IChatHandler
{
    private readonly IHttpClientFactory _http;
    public AnthropicChatHandler(IHttpClientFactory http) => _http = http;

    public HandlerType ProviderType => HandlerType.Anthropic;

    // Anthropic requires max_tokens; retain the existing protocol fallback.
    private const int RequiredMaxTokens = 8192;

    public async IAsyncEnumerable<string> Stream(ChatRequest chatBody, ModelKey modelKey,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var client = new AnthropicClient(
            modelKey.SupplierKey!.ApiKey, _http.CreateClient("AiProvider"));
        var parameters = new MessageParameters()
        {
            Messages = TransferObject(chatBody.Messages!),
            MaxTokens = RequiredMaxTokens,
            Model = modelKey.Model,
            Stream = true,

        };
        var finished = false;
        await foreach (var res in client.Messages.StreamClaudeMessageAsync(parameters, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            finished |= res.Type == "message_stop" || !string.IsNullOrEmpty(res.StopReason) || !string.IsNullOrEmpty(res.Delta?.StopReason);
            if (res.Delta != null)
            {
                if (res.Delta.Text != null)
                {
                    yield return res.Delta.Text;
                }
                
            }
        }
        if (!finished) throw new EndOfStreamException("Anthropic stream ended without a stop event.");
    }

    private static List<AnthropicMessage> TransferObject(IEnumerable<Turbo.Auth.Application.Chat.Message> messages)
    {
        var ms = new List<AnthropicMessage>();
        foreach (var message in messages)
        {
            switch (message.Role!.ToLower()!)
            {
                case OpenAiRole.SystemRole:
                    ms.Add(new AnthropicMessage()
                    {
                        Role = RoleType.User,
                        Content = [new TextContent { Text = message.Content as string ?? string.Empty }]
                    });
                    break;
                case OpenAiRole.UserRole:
                    ms.Add(new AnthropicMessage()
                    {
                        Role = RoleType.User,
                        Content = [new TextContent { Text = message.Content as string ?? string.Empty }]
                    });
                    break;
                case OpenAiRole.Assistant:
                    ms.Add(new AnthropicMessage()
                    {
                        Role = RoleType.Assistant,
                        Content = [new TextContent { Text = message.Content as string ?? string.Empty }]
                    });
                    break;
                default:
                    ms.Add(new AnthropicMessage()
                    {
                        Role = RoleType.User,
                        Content = [new TextContent { Text = message.Content as string ?? string.Empty }]
                    });
                    break;
            }
        }
        return ms;
    }
}
