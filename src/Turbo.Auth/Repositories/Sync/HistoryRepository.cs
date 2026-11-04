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
        return await _context.ChatHistories!.Where(h => h.UserId == userId).ToListAsync();
    }

    public async Task<long> GetHistoryIdByProvides(int userId, long dataId)
    {
        // foreach (var chatHistory in _context.ChatHistories!.ToList())
        // {
        //     Console.WriteLine(chatHistory);
        // }
        var history =  await _context.ChatHistories!.Where(h => h.UserId == userId && h.DataId == dataId).FirstOrDefaultAsync();
        return history?.ChatHistoryId ?? -1;
    }

    public async Task<List<ChatHistory>> GetHistories(int userId,List<long> historyDataIds)
    {
        return await _context.ChatHistories!.AsNoTracking()
            .Where(history => history.UserId == userId && !historyDataIds.Contains(history.DataId))
            .OrderBy(history => history.DataId)
            .ToListAsync();
    }
}
