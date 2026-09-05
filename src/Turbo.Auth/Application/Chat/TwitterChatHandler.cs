using Turbo.Auth.Controllers.AI.Chat.Models;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Models.AI.Chat;

namespace Turbo.Auth.Application.Chat;

public class TwitterChatHandler: IChatHandler
{
    public Turbo.Auth.Application.Providers.HandlerType ProviderType => Turbo.Auth.Application.Providers.HandlerType.Twitter;

    public Task Chat(NoModelChatBody chatBody, ModelKey modelKey, HttpResponse response,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
