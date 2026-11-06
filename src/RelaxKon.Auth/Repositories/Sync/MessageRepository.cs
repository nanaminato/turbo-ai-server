

using Microsoft.EntityFrameworkCore;
using RelaxKon.Auth.Data.Contexts;
using RelaxKon.Auth.Models.Sync.Messages;

namespace RelaxKon.Auth.Repositories.Sync;

public class MessageRepository: IMessageRepository
{
    private AuthContext _context;

    public MessageRepository(AuthContext context, IHistoryRepository historyRepository)
    {
        _context = context;
    }
    public async Task<List<ChatMessage>> GetMessages(int userId, long historyDataId, List<long> messageIds)
    {
        return await _context.ChatMessages!.AsNoTracking()
            .Where(message => message.ChatHistory != null && message.ChatHistory.UserId == userId &&
                message.ChatHistory.DataId == historyDataId && !messageIds.Contains(message.DataId))
            .Include(message => message.FileList)
            .OrderBy(message => message.DataId)
            .ToListAsync();
    }

}
