using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using RelaxKon.Auth.Application.Sync;
using RelaxKon.Auth.Contracts.Sync;
using RelaxKon.Auth.Contracts.Common;

using RelaxKon.Auth.Security.FeatureAccess;

namespace RelaxKon.Auth.Controllers.Sync;

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
        catch (SyncConflictException) { return Conflict(new CodeResponse("sync_conflict")); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException) { return BadRequest(new CodeResponse("invalid_sync_operation")); }
    }
    [HttpPost("history")]
    public Task<IActionResult> ReceiveHistory(HistoryWriteRequest history) => Apply("history", "upsert", history.DataId.ToString(), history);
    [HttpPut("history")]
    public Task<IActionResult> UpdateHistory(HistoryWriteRequest history) => Apply("history", "upsert", history.DataId.ToString(), history);
    [HttpPost("message")]
    public Task<IActionResult> ReceiveMessage(MessageWriteRequest message) => Apply("message", "upsert", message.DataId.ToString(), message, message.HistoryDataId);
    [HttpPut("message")]
    public Task<IActionResult> UpdateMessage(MessageWriteRequest message) => Apply("message", "upsert", message.DataId.ToString(), message, message.HistoryDataId);
    [HttpPost("task")]
    public Task<IActionResult> ReceiveTask(TaskWriteRequest task) => Apply("task", "upsert", task.TaskId, task);
    [HttpPut("task")]
    public Task<IActionResult> UpdateTask(TaskWriteRequest task) => Apply("task", "upsert", task.TaskId, task);
    [HttpDelete("history/{historyDataId}")]
    public Task<IActionResult> DeleteHistory(long historyDataId) => Apply("history", "delete", historyDataId.ToString());
    [HttpDelete("message/{historyDataId}/{messageDataId}")]
    public Task<IActionResult> DeleteMessage(long historyDataId, long messageDataId) => Apply("message", "delete", messageDataId.ToString(), historyId: historyDataId);
    [HttpDelete("task/{taskId}")]
    public Task<IActionResult> DeleteTask(string taskId) => Apply("task", "delete", taskId);
}
