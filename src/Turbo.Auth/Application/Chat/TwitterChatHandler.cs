using Turbo.Auth.Application.Routing;
using Turbo.Auth.Models.AI.Chat;

namespace Turbo.Auth.Application.Chat;

public class TwitterChatHandler: IChatHandler
{
    public Turbo.Auth.Application.Providers.HandlerType ProviderType => Turbo.Auth.Application.Providers.HandlerType.Twitter;

    public IAsyncEnumerable<string> Stream(ChatRequest chatBody, ModelKey modelKey,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
