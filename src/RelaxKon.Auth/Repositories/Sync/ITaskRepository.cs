using RelaxKon.Auth.Models.Sync.Tasks;

namespace RelaxKon.Auth.Repositories.Sync;

public interface ITaskRepository
{
    Task<List<GenerateTask>> GetGenerateTasks(int userId, List<string>? taskIds);
}