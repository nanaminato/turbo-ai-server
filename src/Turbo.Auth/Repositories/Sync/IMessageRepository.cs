using Turbo.Auth.Models.Sync.Messages;

namespace Turbo.Auth.Repositories.Sync;

public interface IMessageRepository
{
    Task<List<ChatMessage>> GetMessages(int userId, long historyDataId, List<long> messageIds);
}
