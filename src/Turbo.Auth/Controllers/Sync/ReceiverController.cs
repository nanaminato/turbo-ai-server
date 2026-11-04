using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Turbo.Auth.Application.Sync;
using Turbo.Auth.Models.Sync.Messages;
using Turbo.Auth.Models.Sync.Tasks;
using Turbo.Auth.Security.FeatureAccess;

namespace Turbo.Auth.Controllers.Sync;

// Compatibility endpoints also enter the change log, so older clients cannot bypass replication.
[ApiController, Authorize(Policy = "user"), Route("api/[controller]")]
public sealed class ReceiverController(DataSyncService sync) : ControllerBase
{
    private async Task<IActionResult> Apply(string kind, string action, string id, object? payload = null, long? historyId = null)
    {
        try
        {
            await sync.Apply(FeatureIdentity.AccountId(User)!.Value,
                new SyncMutation(Guid.NewGuid(), kind, action, id, historyId, payload is null ? null : JToken.FromObject(payload)), HttpContext.RequestAborted);
            return Ok();
        }
        catch (SyncConflictException) { return Conflict(new {code = "sync_conflict"}); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException) { return BadRequest(new {code = "invalid_sync_operation"}); }
    }
    [HttpPost("history")]
    public Task<IActionResult> ReceiveHistory(ChatHistory history) => Apply("history", "upsert", history.DataId.ToString(), history);
    [HttpPut("history")]
    public Task<IActionResult> UpdateHistory(ChatHistory history) => Apply("history", "upsert", history.DataId.ToString(), history);
    [HttpPost("message")]
    public Task<IActionResult> ReceiveMessage(MMessage message) => Apply("message", "upsert", message.DataId.ToString(), message, message.HistoryDataId);
    [HttpPut("message")]
    public Task<IActionResult> UpdateMessage(MMessage message) => Apply("message", "upsert", message.DataId.ToString(), message, message.HistoryDataId);
    [HttpPost("task")]
    public Task<IActionResult> ReceiveTask(GenerateTask task) => Apply("task", "upsert", task.TaskId, task);
    [HttpPut("task")]
    public Task<IActionResult> UpdateTask(GenerateTask task) => Apply("task", "upsert", task.TaskId, task);
    [HttpDelete("history/{historyDataId}")]
    public Task<IActionResult> DeleteHistory(long historyDataId) => Apply("history", "delete", historyDataId.ToString());
    [HttpDelete("message/{historyDataId}/{messageDataId}")]
    public Task<IActionResult> DeleteMessage(long historyDataId, long messageDataId) => Apply("message", "delete", messageDataId.ToString(), historyId: historyDataId);
    [HttpDelete("task/{taskId}")]
    public Task<IActionResult> DeleteTask(string taskId) => Apply("task", "delete", taskId);
}
