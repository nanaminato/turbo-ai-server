using Turbo_Auth.Handlers.Differentiator;

namespace Turbo_Auth.Handlers.Chat;

public class ChatHandlerObtain: IChatHandlerObtain
{
    public ChatHandlerObtain()
    {
    }
    public IChatHandler GetHandler(HandlerType handlerType)
    {
        return handlerType switch
        {
            HandlerType.Google => new GoogleChatHandler(),
            HandlerType.Openai => new OpenAiChatHandler(),
            HandlerType.Anthropic => new AnthropicChatHandler(),
            HandlerType.Alibaba => new AlibabaChatHandler(),
            HandlerType.Twitter => new TwitterChatHandler(),
            _ => new OpenAiChatHandler()
        };
    }
}