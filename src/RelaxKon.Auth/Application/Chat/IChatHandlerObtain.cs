using RelaxKon.Auth.Application.Providers;

namespace RelaxKon.Auth.Application.Chat;

public interface IChatHandlerObtain
{
    public IChatHandler GetHandler(HandlerType handlerType);
}