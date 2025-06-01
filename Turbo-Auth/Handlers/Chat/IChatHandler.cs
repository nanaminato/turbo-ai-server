using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Ai;
using Turbo_Auth.Models.Ai.Chat;

namespace Turbo_Auth.Handlers.Chat;

public interface IChatHandler
{
    Task Chat(NoModelChatBody chatBody, ModelKey modelKey, HttpResponse response);
}