using RelaxKon.Auth.Models.Sync.Messages;
namespace RelaxKon.Auth.Repositories.Sync;

// Read-side compatibility queries. All mutations enter DataSyncService.
public interface IHistoryRepository
{
    Task<List<ChatHistory>> GetHistoriesByUserId(long userId);
    Task<long> GetHistoryIdByProvides(int userId, long dataId);
    Task<List<ChatHistory>> GetHistories(int userId, List<long> historyDataIds);
}
