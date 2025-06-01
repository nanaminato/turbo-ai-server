using Turbo_Auth.Handlers.Differentiator;

namespace Turbo_Auth.Handlers.Chat;

public interface IChatHandlerObtain
{
    public IChatHandler GetHandler(HandlerType handlerType);
}