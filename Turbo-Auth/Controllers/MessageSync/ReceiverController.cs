using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo_Auth.Models.ClientSyncs.Messages;
using Turbo_Auth.Models.Mediators.Messages;
using Turbo_Auth.Models.Tasks;
using Turbo_Auth.Repositories.Messages;

namespace Turbo_Auth.Controllers.MessageSync;

[ApiController]
[Authorize(Policy = "user")]
[Route("api/[controller]")]
public class ReceiverController: Controller
{
    private IHistoryRepository _history;
    private IMessageRepository _message;
    private ITaskRepository _task;
    private IIdGetter _idGetter;
    public ReceiverController(IHistoryRepository history,
        IMessageRepository message, ITaskRepository task, IIdGetter idGetter)
    {
        _history = history;
        _message = message;
        _task = task;
        _idGetter = idGetter;
    }

    [HttpPost("history")]
    public async Task<IActionResult> ReceiveHistory(ChatHistory history)
    {
        var userId = _idGetter.GetId(User);
        history.UserId = userId;
        await _history.AddHistory(history);
        return Ok();
    }

    [HttpPost("message")]
    public async Task<IActionResult> ReceiveMessage(MMessage mMessage)
    {
        var userId = _idGetter.GetId(User);
        mMessage.UserId = userId;
        await _message.AddMessage(mMessage);
        return Ok();
    }

    [HttpPost("task")]
    public async Task<IActionResult> ReceiveTask(GenerateTask task)
    {
        var userId = _idGetter.GetId(User);
        task.AccountId = userId;
        await _task.AddTask(task);
        return Ok();
    }

    [HttpPut("history")]
    public async Task<IActionResult> UpdateHistory(ChatHistory history)
    {
        var userId = _idGetter.GetId(User);
        history.UserId = userId;
        await _history.UpdateHistory(history);
        return Ok();
    }

    [HttpPut("message")]
    public async Task<IActionResult> UpdateMessage(MMessage mMessage)
    {
        var userId = _idGetter.GetId(User);
        mMessage.UserId = userId;
        await _message.UpdateMessage(mMessage);
        return Ok();
    }

    [HttpPut("task")]
    public async Task<IActionResult> UpdateTask(GenerateTask task)
    {
        var userId = _idGetter.GetId(User);
        task.AccountId = userId;
        await _task.UpdateOrInsertTask(task);
        return Ok();
    }

    [HttpDelete("history/{historyDataId}")]
    public async Task<IActionResult> DeleteHistory(long historyDataId)
    {
        var userId = _idGetter.GetId(User);
        var id = await _history.GetHistoryIdByProvides(userId, historyDataId);
        await _history.DeleteHistoryByHistoryId(id);
        return Ok();
    }
    [HttpDelete("message/{historyDataId}/{messageDataId}")]
    public async Task<IActionResult> DeleteMessage(long historyDataId,long messageDataId)
    {
        var userId = _idGetter.GetId(User);
        await _message.DeleteMessage(userId, historyDataId, messageDataId);
        return Ok();
    }

    [HttpDelete("task/{taskId}")]
    public async Task<IActionResult> DeleteTask(string taskId)
    {
        var userId = _idGetter.GetId(User);
        await _task.DeleteTaskByTaskId(taskId, userId);
        return Ok();
    }
}