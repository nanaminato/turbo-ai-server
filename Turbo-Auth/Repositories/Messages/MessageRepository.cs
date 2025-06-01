

using Microsoft.EntityFrameworkCore;
using Turbo_Auth.Context;
using Turbo_Auth.Models.ClientSyncs.Messages;
using Turbo_Auth.Models.Mediators.Messages;

namespace Turbo_Auth.Repositories.Messages;

public class MessageRepository: IMessageRepository
{
    private AuthContext _context;
    private IHistoryRepository _historyRepository;

    public MessageRepository(AuthContext context, IHistoryRepository historyRepository)
    {
        _context = context;
        _historyRepository = historyRepository;
    }
    public async Task AddMessage(MMessage message)
    {
        var historyId = await _historyRepository
            .GetHistoryIdByProvides(message.UserId, message.HistoryDataId);
        var ms = Transform(message);
        ms.ChatHistoryId = historyId;
        await _context.ChatMessages!.AddAsync(ms);
        await _context.SaveChangesAsync();
    }
    /// <summary>
    /// 未经验证，当要更新一个消息时，先删除之前的消息，然后插入这个消息
    /// </summary>
    /// <param name="message"></param>
    public async Task UpdateMessage(MMessage message)
    {
        var historyId = await _historyRepository
            .GetHistoryIdByProvides(message.UserId, message.HistoryDataId);
        var ms = await _context.ChatMessages
            !.FirstOrDefaultAsync(m => m.ChatHistoryId == historyId && m.DataId == message.DataId);
        if (ms != null)
        {
            Console.WriteLine("");
            _context.Remove(ms);
            await _context.SaveChangesAsync();
        }
        var ms2 = Transform(message);
        ms2.ChatHistoryId = historyId;
        await _context.ChatMessages!.AddAsync(ms2);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteMessage(int userId,long historyDataId,long dataId)
    {
         var historyId = await _historyRepository
            .GetHistoryIdByProvides(userId, historyDataId);
         var ms = await _context.ChatMessages
             !.FirstOrDefaultAsync(m => m.ChatHistoryId == historyId && m.DataId == dataId);
         if (ms != null)
         {
             _context.Remove(ms);
             await _context.SaveChangesAsync();
         }
    }

    public async Task<List<ChatMessage>> GetMessages(int userId, long historyDataId, List<long> messageIds)
    {
        var historyId = await _historyRepository
            .GetHistoryIdByProvides(userId, historyDataId);
        var messageInDb = await _context.ChatMessages!
            .Where(m => m.ChatHistoryId == historyId)
            .OrderBy(c=>c.DataId)
            .Select(ch=>ch.DataId)
            .ToListAsync();
        var wants = messageInDb.Except(messageIds).ToList();
        var chatMessages = new List<ChatMessage>();
        foreach (var wantId in wants)
        {
            var chatMessage = await _context.ChatMessages!
                .Include(c=>c.FileList)
                .FirstOrDefaultAsync(c => c.DataId == wantId);
            if (chatMessage != null)
            {
                chatMessages.Add(chatMessage);
            }
        }

        return chatMessages;
    }

    public static ChatMessage Transform(MMessage message)
    {
        return new ChatMessage()
        {
            DataId = message.DataId,
            Role = message.Role,
            Content = message.Content,
            FileList = message.FileList,
            ShowType = message.ShowType,
            Finish = message.Finish,
            Model = message.Model
        };
    }
}