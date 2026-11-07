using RelaxKon.Auth.Contracts.Common;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RelaxKon.Auth.Application.Sync;
namespace RelaxKon.Auth.Contracts.Sync;

public sealed record SyncMutationRequest(
    Guid OperationId, [Required, AllowedValues("history", "message", "task", "languagePack", "uiLanguage")] string Kind,
    [Required, AllowedValues("upsert", "delete")] string Action,
    [Required, StringLength(255)] string EntityId, long? HistoryId = null, JToken? Payload = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (OperationId == Guid.Empty) yield return new("Operation ID must not be empty.", [nameof(OperationId)]);
        if (Action == "upsert" && Payload is not JObject) yield return new("Upserts require an object payload.", [nameof(Payload)]);
    }
    public SyncMutation ToApplication() => new(OperationId, Kind, Action, EntityId, HistoryId, Payload);
}
public sealed record SyncVersionResponse(long Version);
public sealed record SyncBatchResponse(long Cursor, bool Reset, bool HasMore, IReadOnlyList<SyncEventResponse> Changes,
    [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] IReadOnlyList<SyncEventResponse>? LanguagePacks = null)
{
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? UiLanguageSupported { get; init; }

    public static SyncBatchResponse From(SyncBatch batch) => new(batch.Cursor, batch.Reset, batch.HasMore,
        batch.Changes.Select(ToResponse).ToList(), batch.LanguagePacks?.Select(ToResponse).ToList());

    private static SyncEventResponse ToResponse(SyncEvent item) =>
        new(item.Version, item.Kind, item.Action, item.EntityId, item.HistoryId, item.Payload);
}
public sealed record SyncEventResponse(long Version, string Kind, string Action, string EntityId, long? HistoryId, JToken? Payload);
public sealed class RequestTask { public List<string>? TaskIds { get; init; } }
public sealed class RequestMessage
{
    [Range(1, 9007199254740991)] public long HistoryDataId { get; init; }
    [Required] public List<long> MessageIds { get; init; } = [];
}
public sealed class HistoryWriteRequest
{
    [Range(1, 9007199254740991)] public long DataId { get; init; }
    public string? Title { get; init; }
}
public sealed class MessageWriteRequest
{
    [Range(1, 9007199254740991)] public long HistoryDataId { get; init; }
    [Range(1, 9007199254740991)] public long DataId { get; init; }
    public string? Role { get; init; }
    public string? Content { get; init; }
    [NonNullItems] public List<SyncFileContract>? FileList { get; init; }
    public int ShowType { get; init; }
    public bool Finish { get; init; }
    public string? Model { get; init; }
}
public sealed class TaskWriteRequest
{
    [Required, StringLength(255), JsonProperty("task_id")] public string TaskId { get; init; } = "";
    [JsonProperty("task_type")] public string? TaskType { get; init; }
    [JsonProperty("date")] public DateTime? DateTime { get; init; }
    [JsonProperty("images")] public List<string>? Images { get; init; }
    [JsonProperty("videos")] public List<string>? Videos { get; init; }
}
public sealed record SyncFileContract(string? FileName, string? FileType, long FileSize, string? FileContent, string? ParsedContent);
