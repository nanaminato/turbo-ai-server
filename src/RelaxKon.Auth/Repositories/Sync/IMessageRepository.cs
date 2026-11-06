using RelaxKon.Auth.Models.Sync.Messages;

namespace RelaxKon.Auth.Repositories.Sync;

public interface IMessageRepository
{
    Task<List<ChatMessage>> GetMessages(int userId, long historyDataId, List<long> messageIds);
}
