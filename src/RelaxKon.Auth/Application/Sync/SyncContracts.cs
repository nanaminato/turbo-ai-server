using Newtonsoft.Json.Linq;

namespace RelaxKon.Auth.Application.Sync;

public sealed record SyncMutation(Guid OperationId, string Kind, string Action, string EntityId,
    long? HistoryId = null, JToken? Payload = null);
public sealed record SyncEvent(long Version, string Kind, string Action, string EntityId,
    long? HistoryId, JToken? Payload);
public sealed record SyncBatch(long Cursor, bool Reset, bool HasMore, IReadOnlyList<SyncEvent> Changes,
    IReadOnlyList<SyncEvent>? LanguagePacks = null);
public sealed class SyncConflictException(string message) : Exception(message);

public sealed class SyncAccount
{
    public int AccountId { get; set; }
    public long Version { get; set; }
}
public sealed class SyncChange
{
    public int AccountId { get; set; }
    public long Version { get; set; }
    public string OperationId { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Action { get; set; } = "";
    public string EntityId { get; set; } = "";
    public long? HistoryId { get; set; }
    public string? Payload { get; set; }
}
