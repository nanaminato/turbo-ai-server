using Microsoft.EntityFrameworkCore;
using Turbo_Auth.Context;
using Turbo_Auth.Models.Tasks;

namespace Turbo_Auth.Repositories.Messages;

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
        var query = _authContext.Set<GenerateTask>()
            .Where(t => t.AccountId == userId);

        // 如果 taskIds 不为空，则过滤掉包含在其中的 taskId
        if (taskIds != null && taskIds.Any())
        {
            // 核心逻辑：t.TaskId 不在给定的 taskIds 列表中
            query = query.Where(t => !taskIds.Contains(t.TaskId));
        }

        return await query
            .OrderByDescending(t => t.DateTime)
            .ToListAsync();
    }


    /// <summary>
    /// 根据任务ID和用户ID删除任务（确保用户只能删除自己的任务）
    /// </summary>
    public async Task DeleteTaskByTaskId(string taskId, int userId)
    {
        var task = await _authContext.Set<GenerateTask>()
            .FirstOrDefaultAsync(t => t.TaskId == taskId && t.AccountId == userId);
        if (task != null)
        {
            _authContext.Set<GenerateTask>().Remove(task);
            await _authContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 添加新任务
    /// </summary>
    public async Task AddTask(GenerateTask generateTask)
    {
        try
        {
            await _authContext.Set<GenerateTask>().AddAsync(generateTask);
            await _authContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // 检查是否是由于重复键引起的冲突
            // 这里的判断逻辑取决于你使用的数据库（MySQL, SQL Server, PostgreSQL 等）
            if (IsDuplicateKeyException(ex))
            {
                throw new Exception("该任务已存在，请勿重复添加。");
            }
            throw;
        }
    }

    private bool IsDuplicateKeyException(DbUpdateException ex)
    {
        // 简单示例：检查内部异常消息
        return ex.InnerException != null && 
               (ex.InnerException.Message.Contains("Duplicate") || 
                ex.InnerException.Message.Contains("unique constraint"));
    }

    /// <summary>
    /// 更新任务
    /// </summary>
    public async Task UpdateOrInsertTask(GenerateTask generateTask)
    {
        _authContext.Entry(generateTask).State = EntityState.Modified;

        try
        {
            await _authContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // 如果是因为记录不存在导致的并发异常
            var exists = await _authContext.GenerateTasks
                .AnyAsync(t => t.TaskId == generateTask.TaskId);

            if (!exists)
            {
                // 改变状态为 Added 并重新保存
                _authContext.Entry(generateTask).State = EntityState.Added;
                await _authContext.SaveChangesAsync();
            }
            else
            {
                throw; // 真的是并发冲突（数据存在但版本不对）
            }
        }
    }


    // 辅助方法：检查任务是否存在
    private bool TaskExists(string? taskId)
    {
        return _authContext.Set<GenerateTask>().Any(e => e.TaskId == taskId);
    }
}