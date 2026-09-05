using Turbo_Auth.Controllers.Ai.Chat.Models;
using Turbo_Auth.Handlers.Differentiator;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Ai;
using Turbo_Auth.Models.Ai.Chat;

namespace Turbo_Auth.Handlers.Chat;

public interface IChatHandler
{
    HandlerType ProviderType { get; }

    Task Chat(NoModelChatBody chatBody, ModelKey modelKey, HttpResponse response,
        CancellationToken cancellationToken);
}
