using RelaxKon.Auth.Application.Providers;
using RelaxKon.Auth.Application.Routing;
using RelaxKon.Auth.Models.AI;
using RelaxKon.Auth.Models.AI.Chat;

namespace RelaxKon.Auth.Application.Chat;

public interface IChatHandler
{
    HandlerType ProviderType { get; }

    IAsyncEnumerable<string> Stream(ChatRequest chatBody, ModelKey modelKey, CancellationToken cancellationToken);
}
