using Microsoft.EntityFrameworkCore;
using Turbo.Auth.Data.Contexts;
using Turbo.Auth.Models.Sync.Tasks;

namespace Turbo.Auth.Repositories.Sync;

public class TaskRepository : ITaskRepository
{
    private AuthContext _authContext;
    public TaskRepository(AuthContext authContext)
    {
        _authContext = authContext;
    }
    /// <summary>
    /// 获取指定用户的所有任务
    /// </summary>
    public async Task<List<GenerateTask>> GetGenerateTasks(int userId, List<string>? taskIds)
    {
        var query = _authContext.Set<GenerateTask>().AsNoTracking()
            .Where(t => t.AccountId == userId);

        // 如果 taskIds 不为空，则过滤掉包含在其中的 taskId
        if (taskIds != null && taskIds.Any())
        {
            // 核心逻辑：t.TaskId 不在给定的 taskIds 列表中
            query = query.Where(t => !taskIds.Contains(t.TaskId));
        }

        return await query
            .OrderBy(t => t.DateTime)
            .ThenBy(t => t.TaskId)
            .ToListAsync();
    }


}
