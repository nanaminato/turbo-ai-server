using Turbo_Auth.Handlers.Differentiator;

namespace Turbo_Auth.Handlers.Chat;

public class ChatHandlerObtain: IChatHandlerObtain
{
    private readonly IReadOnlyDictionary<HandlerType, IChatHandler> _handlers;

    public ChatHandlerObtain(IEnumerable<IChatHandler> handlers)
    {
        _handlers = handlers.ToDictionary(handler => handler.ProviderType);
    }

    public IChatHandler GetHandler(HandlerType handlerType)
    {
        if (_handlers.TryGetValue(handlerType, out var handler))
        {
            return handler;
        }

        throw new NotSupportedException($"Provider '{handlerType}' does not support chat requests.");
    }
}
