using Turbo.Auth.Models.Sync.Tasks;

namespace Turbo.Auth.Repositories.Sync;

public interface ITaskRepository
{
    Task<List<GenerateTask>> GetGenerateTasks(int userId, List<string>? taskIds);
    Task DeleteTaskByTaskId(string taskId, int userId);
    Task AddTask(GenerateTask generateTask);
    Task UpdateOrInsertTask(GenerateTask generateTask);
}