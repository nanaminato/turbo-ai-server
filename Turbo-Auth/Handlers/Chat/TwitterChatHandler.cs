using Turbo_Auth.Controllers.Ai.Chat.Models;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Ai.Chat;

namespace Turbo_Auth.Handlers.Chat;

public class TwitterChatHandler: IChatHandler
{
    public Handlers.Differentiator.HandlerType ProviderType => Handlers.Differentiator.HandlerType.Twitter;

    public Task Chat(NoModelChatBody chatBody, ModelKey modelKey, HttpResponse response,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
