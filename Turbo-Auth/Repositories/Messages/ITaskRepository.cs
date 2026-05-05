using Turbo_Auth.Models.Tasks;

namespace Turbo_Auth.Repositories.Messages;

public interface ITaskRepository
{
    Task<List<GenerateTask>> GetGenerateTasks(int userId, List<string>? taskIds);
    Task DeleteTaskByTaskId(string taskId, int userId);
    Task AddTask(GenerateTask generateTask);
    Task UpdateOrInsertTask(GenerateTask generateTask);
}