using Turbo.Auth.Models.Sync.Messages;

namespace Turbo.Auth.Repositories.Sync;

public interface IMessageRepository
{
    Task AddMessage(MMessage message);
    Task UpdateMessage(MMessage message);
    Task DeleteMessage(int userId,long historyDataId,long dataId);
    Task<List<ChatMessage>> GetMessages(int userId, long historyDataId, List<long> messageIds);
}
