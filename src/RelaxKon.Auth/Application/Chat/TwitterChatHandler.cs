using RelaxKon.Auth.Application.Routing;
using RelaxKon.Auth.Models.AI.Chat;

namespace RelaxKon.Auth.Application.Chat;

public class TwitterChatHandler: IChatHandler
{
    public RelaxKon.Auth.Application.Providers.HandlerType ProviderType => RelaxKon.Auth.Application.Providers.HandlerType.Twitter;

    public IAsyncEnumerable<string> Stream(ChatRequest chatBody, ModelKey modelKey,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
