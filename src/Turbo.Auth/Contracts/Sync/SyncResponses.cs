using Newtonsoft.Json;
using Turbo.Auth.Models.Sync.Messages;
using Turbo.Auth.Models.Sync.Tasks;
namespace Turbo.Auth.Contracts.Sync;

public sealed record HistoryResponse(long ChatHistoryId, int UserId, long DataId, string? Title)
{
    public static HistoryResponse From(ChatHistory history) => new(history.ChatHistoryId, history.UserId, history.DataId, history.Title);
}
public sealed record SyncedMessageResponse(long ChatMessageId, long DataId, long ChatHistoryId, string? Role, string? Content,
    IReadOnlyList<SyncFileContract>? FileList, int ShowType, bool Finish, string? Model)
{
    public static SyncedMessageResponse From(ChatMessage message) => new(message.ChatMessageId, message.DataId, message.ChatHistoryId,
        message.Role, message.Content, message.FileList?.Select(file => new SyncFileContract(file.FileName, file.FileType, file.FileSize,
            file.FileContent, file.ParsedContent)).ToList(), message.ShowType, message.Finish, message.Model);
}
public sealed record SyncedTaskResponse([property: JsonProperty("task_id")] string TaskId,
    [property: JsonProperty("task_type")] string? TaskType, [property: JsonProperty("date")] DateTime? DateTime,
    [property: JsonProperty("account_id")] int AccountId, [property: JsonProperty("images")] List<string>? Images,
    [property: JsonProperty("videos")] List<string>? Videos)
{
    public static SyncedTaskResponse From(GenerateTask task) => new(task.TaskId, task.TaskType, task.DateTime, task.AccountId, task.Images, task.Videos);
}
