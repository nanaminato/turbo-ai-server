using Microsoft.EntityFrameworkCore;
using Turbo.Auth.Data.Contexts;
using Turbo.Auth.Models.Sync.Messages;

namespace Turbo.Auth.Repositories.Sync;

public class HistoryRepository: IHistoryRepository
{
    private AuthContext _context;
    public HistoryRepository(AuthContext context)
    {
        _context = context;
    }
    public async Task<List<ChatHistory>> GetHistoriesByUserId(long userId)
    {
        return await _context.ChatHistories!.AsNoTracking().Where(h => h.UserId == userId).ToListAsync();
    }

    public async Task<long> GetHistoryIdByProvides(int userId, long dataId)
    {
        return await _context.ChatHistories!.AsNoTracking()
            .Where(history => history.UserId == userId && history.DataId == dataId)
            .Select(history => (long?)history.ChatHistoryId).FirstOrDefaultAsync() ?? -1;
    }

    public async Task<List<ChatHistory>> GetHistories(int userId,List<long> historyDataIds)
    {
        return await _context.ChatHistories!.AsNoTracking()
            .Where(history => history.UserId == userId && !historyDataIds.Contains(history.DataId))
            .OrderBy(history => history.DataId)
            .ToListAsync();
    }
}
