

using Microsoft.EntityFrameworkCore;
using Turbo.Auth.Data.Contexts;
using Turbo.Auth.Models.Sync.Messages;

namespace Turbo.Auth.Repositories.Sync;

public class MessageRepository: IMessageRepository
{
    private AuthContext _context;
    private IHistoryRepository _historyRepository;

    public MessageRepository(AuthContext context, IHistoryRepository historyRepository)
    {
        _context = context;
        _historyRepository = historyRepository;
    }
    public async Task<List<ChatMessage>> GetMessages(int userId, long historyDataId, List<long> messageIds)
    {
        var historyId = await _historyRepository
            .GetHistoryIdByProvides(userId, historyDataId);
        return await _context.ChatMessages!.AsNoTracking()
            .Where(message => message.ChatHistoryId == historyId && !messageIds.Contains(message.DataId))
            .Include(message => message.FileList)
            .OrderBy(message => message.DataId)
            .ToListAsync();
    }

}
