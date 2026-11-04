using Turbo.Auth.Models.Sync.Tasks;

namespace Turbo.Auth.Repositories.Sync;

public interface ITaskRepository
{
    Task<List<GenerateTask>> GetGenerateTasks(int userId, List<string>? taskIds);
}