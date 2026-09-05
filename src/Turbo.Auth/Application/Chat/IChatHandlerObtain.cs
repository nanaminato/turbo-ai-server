using Turbo.Auth.Application.Providers;

namespace Turbo.Auth.Application.Chat;

public interface IChatHandlerObtain
{
    public IChatHandler GetHandler(HandlerType handlerType);
}