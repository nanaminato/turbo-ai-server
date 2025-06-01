using Microsoft.EntityFrameworkCore;
using Turbo_Auth.Context;
using Turbo_Auth.Models.ClientSyncs.Messages;

namespace Turbo_Auth.Repositories.Messages;

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

    public async Task DeleteHistoryByHistoryId(long historyId)
    {
        var history = await _context.ChatHistories!.Where(h => h.ChatHistoryId == historyId).FirstOrDefaultAsync();
        if (history != null)
        {
            _context.ChatHistories!.Remove(history);
            await _context.SaveChangesAsync();
        }
        
    }

    public async Task AddHistory(ChatHistory history)
    {
        await _context.AddAsync(history);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateHistory(ChatHistory history)
    {
        var oldHistory = await _context.ChatHistories!
            .Where(h => h.UserId == history.UserId && h.DataId == history.DataId)
            .FirstOrDefaultAsync();
        if (oldHistory != null)
        {
            oldHistory.Title = history.Title;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<ChatHistory>> GetHistories(int userId,List<long> historyDataIds)
    {
        var historyIdsInDb = await _context.ChatHistories!
            .Where(c=>c.UserId==userId)
            .OrderBy(c=>c.DataId)
            .Select(c=>c.DataId)
            .ToListAsync();
        var c = historyIdsInDb.Except(historyDataIds).ToList();
        var chatHistories = new List<ChatHistory>();
        foreach (var id in c)
        {
            var chatHistory = await _context.ChatHistories!.FirstOrDefaultAsync(ch => ch.DataId == id);
            if (chatHistory != null)
            {
                chatHistories.Add(chatHistory);
            }
        }

        return chatHistories;
    }
}