using Turbo_Auth.Models.ClientSyncs.Messages;

namespace Turbo_Auth.Repositories.Messages;

public interface IHistoryRepository
{
    // 根据用户id获取所有的聊天历史对象（也可以进行关联拉取所有的消息）
    // 可以以延迟加载的形式向用户提供，当用户登录或者创建连接之后，首先拉取
    // 聊天列表，然后用户点击某个列表时再次进行拉取操作
    // 同时只有在响应时对用户的消息进行存储，构建一个无状态的同步器
    // 这样就无需同步器，虽然有一定的同步漏洞，但是实现起来比较简单
    Task<List<ChatHistory>> GetHistoriesByUserId(long userId);
    // 根据用户Id和原始聊天历史id获取到数据库中的聊天历史id
    Task<long> GetHistoryIdByProvides(int userId, long dataId);
    Task DeleteHistoryByHistoryId(long historyId);
    Task AddHistory(ChatHistory history);
    Task UpdateHistory(ChatHistory history);
    Task<List<ChatHistory>> GetHistories(int userId,List<long> historyDataIds);
}