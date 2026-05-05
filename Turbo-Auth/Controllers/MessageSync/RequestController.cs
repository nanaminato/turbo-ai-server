using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo_Auth.Repositories.Messages;

namespace Turbo_Auth.Controllers.MessageSync;

[ApiController]
[Authorize(Policy = "user")]
[Route("api/[controller]")]
public class RequestController: Controller
{
    private IHistoryRepository _history;
    private IMessageRepository _message;
    private ITaskRepository _task;
    private IIdGetter _idGetter;

    public RequestController(IHistoryRepository history,
        IMessageRepository message, ITaskRepository task, IIdGetter idGetter)
    {
        _history = history;
        _message = message;
        _task = task;
        _idGetter = idGetter;
    }

    /**
     请求所有的聊天列表，参数： 已经具有的聊天列表的浏览器时间id，
     我们需要排除这些Id，然后传递没有这些id的聊天列表.
     此外，我们还需要获得用户的id用于查找具体的聊天列表
    **/
    [HttpPost("histories")]
    public async Task<IActionResult> GetHistories(List<long> historyDataIds)
    {
        var id = _idGetter.GetId(User);
        var histories = await _history.GetHistories(id,historyDataIds);
        return Ok(histories);
    }
    
    /* 请求一个聊天会话的信息
     */
    [HttpPost("messages")]
    public async Task<IActionResult> GetMessages(RequestMessage request)
    {
        var id = _idGetter.GetId(User);
        var messages = await _message.GetMessages(id,request.HistoryDataId,request.MessageIds!);
        return Ok(messages);
    }

    [HttpPost("tasks")]
    public async Task<IActionResult> GetTasks(RequestTask request)
    {
        var id = _idGetter.GetId(User);
        var tasks = await _task.GetGenerateTasks(id, request.TaskIds);
        return Ok(tasks);
    }
}

public class RequestTask
{
    public List<string>? TaskIds
    {
        get;
        set;
    }
}

public class RequestMessage
{
    public long HistoryDataId
    {
        get;
        set;
    }

    public List<long>? MessageIds
    {
        get;
        set;
    }
    public override string ToString()
    {
        return $"RequestMessage: HistoryDataId={HistoryDataId}, MessageIds={(MessageIds != null ? string.Join(",", MessageIds) : "null")}";
    }
    
}