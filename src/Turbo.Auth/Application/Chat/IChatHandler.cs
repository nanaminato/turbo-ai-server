using Turbo.Auth.Controllers.AI.Chat.Models;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Models.AI;
using Turbo.Auth.Models.AI.Chat;

namespace Turbo.Auth.Application.Chat;

public interface IChatHandler
{
    HandlerType ProviderType { get; }

    Task Chat(NoModelChatBody chatBody, ModelKey modelKey, HttpResponse response,
        CancellationToken cancellationToken);
}
