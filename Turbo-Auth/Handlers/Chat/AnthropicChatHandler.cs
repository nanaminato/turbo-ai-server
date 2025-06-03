using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Ai;
using Turbo_Auth.Models.Ai.Chat;
using Message = Anthropic.SDK.Messaging.Message;

namespace Turbo_Auth.Handlers.Chat;

public class AnthropicChatHandler: IChatHandler
{
    public async Task Chat(NoModelChatBody chatBody, ModelKey modelKey, HttpResponse response)
    {
        var client = new AnthropicClient(
            modelKey.SupplierKey!.ApiKey);
        var parameters = new MessageParameters()
        {
            Messages = TransferObject(chatBody.Messages!),
            MaxTokens = chatBody.MaxCompletionTokens??4000,
            Model = modelKey.Model,
            Stream = true,
            Temperature = chatBody.Temperature.HasValue ? (decimal?)chatBody.Temperature.Value : null,
            TopP = chatBody.TopP.HasValue?(decimal?)chatBody.TopP.Value: null,
            
        };
        await foreach (var res in client.Messages.StreamClaudeMessageAsync(parameters))
        {
            if (res.Delta != null)
            {
                if (res.Delta.Text != null)
                {
                    await response.WriteAsync(res.Delta.Text);
                }
                
            }
        }
    }

    private static List<Message> TransferObject(IEnumerable<Models.Ai.Chat.Message> messages)
    {
        var ms = new List<Message>();
        foreach (var message in messages)
        {
            switch (message.Role!.ToLower()!)
            {
                case OpenAiRole.SystemRole:
                    ms.Add(new Message()
                    {
                        Role = RoleType.User,
                        Content = message.Content
                    });
                    break;
                case OpenAiRole.UserRole:
                    ms.Add(new Message()
                    {
                        Role = RoleType.User,
                        Content = message.Content
                    });
                    break;
                case OpenAiRole.Assistant:
                    ms.Add(new Message()
                    {
                        Role = RoleType.Assistant,
                        Content = message.Content
                    });
                    break;
                default:
                    ms.Add(new Message()
                    {
                        Role = RoleType.User,
                        Content = message.Content
                    });
                    break;
            }
        }
        return ms;
    }
}