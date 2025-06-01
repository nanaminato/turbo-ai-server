using Turbo_Auth.Models.ClientSyncs.Messages;
using Turbo_Auth.Models.Mediators.Messages;

namespace Turbo_Auth.Repositories.Messages;

public interface IMessageRepository
{
    Task AddMessage(MMessage message);
    Task UpdateMessage(MMessage message);
    Task DeleteMessage(int userId,long historyDataId,long dataId);
    Task<List<ChatMessage>> GetMessages(int userId, long historyDataId, List<long> messageIds);
}