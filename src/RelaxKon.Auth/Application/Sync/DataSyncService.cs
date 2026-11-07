using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using RelaxKon.Auth.Data.Contexts;
using RelaxKon.Auth.Models.Sync.Messages;
using RelaxKon.Auth.Models.Sync.Tasks;

namespace RelaxKon.Auth.Application.Sync;

/// <summary>Account-scoped commit order, durable tombstones and idempotent writes.</summary>
public sealed class DataSyncService(AuthContext database)
{
    private static JToken Json(object value) => JToken.FromObject(value, JsonSerializer.Create(new JsonSerializerSettings
    { ContractResolver = new CamelCasePropertyNamesContractResolver() }));
    private bool MySql => database.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true;

    private async Task<SyncAccount> LockAccount(int accountId, CancellationToken token)
    {
        if (MySql)
            await database.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SyncAccounts (AccountId,Version) VALUES ({accountId},0) ON DUPLICATE KEY UPDATE AccountId=AccountId", token);
        else
            await database.Database.ExecuteSqlInterpolatedAsync($"INSERT OR IGNORE INTO SyncAccounts (AccountId,Version) VALUES ({accountId},0)", token);
        // The insert/upsert already owns the account row lock. A tracking query alone does not
        // refresh an entity materialized by an earlier operation on this DbContext.
        var tracked = database.ChangeTracker.Entries<SyncAccount>().SingleOrDefault(entry => entry.Entity.AccountId == accountId);
        if (tracked is not null) await tracked.ReloadAsync(token);
        // The lock is held through the data write, log append and commit, so a cursor never skips a late commit.
        if (MySql)
            return (await database.Set<SyncAccount>().FromSqlInterpolated($"SELECT AccountId,Version FROM SyncAccounts WHERE AccountId={accountId} FOR UPDATE")
                .AsTracking().ToListAsync(token)).Single();
        return await database.Set<SyncAccount>().SingleAsync(item => item.AccountId == accountId, token);
    }

    public async Task<long> Apply(int accountId, SyncMutation mutation, CancellationToken token)
    {
        if (mutation.OperationId == Guid.Empty || mutation.Kind is not ("history" or "message" or "task" or "languagePack" or "uiLanguage") ||
            mutation.Action is not ("upsert" or "delete") || string.IsNullOrWhiteSpace(mutation.EntityId) || mutation.EntityId.Length > 255)
            throw new ArgumentException("Invalid sync operation.");
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(mutation))));
        await using var transaction = await database.Database.BeginTransactionAsync(MySql ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, token);
        var state = await LockAccount(accountId, token);
        var operationId = mutation.OperationId.ToString("N");
        var existing = await database.Set<SyncChange>().SingleOrDefaultAsync(item => item.AccountId == accountId && item.OperationId == operationId, token);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash) throw new SyncConflictException("Operation ID was reused with a different payload.");
            await transaction.CommitAsync(token);
            return existing.Version;
        }
        var payload = mutation.Kind switch
        {
            "history" => await History(accountId, mutation, token),
            "message" => await Message(accountId, mutation, token),
            "languagePack" => LanguagePack(mutation),
            "uiLanguage" => await UiLanguage(accountId, mutation, token),
            _ => await Task(accountId, mutation, token)
        };
        state.Version++;
        database.Set<SyncChange>().Add(new SyncChange
        {
            AccountId = accountId,
            Version = state.Version,
            OperationId = operationId,
            RequestHash = requestHash,
            Kind = mutation.Kind,
            Action = mutation.Action,
            EntityId = mutation.EntityId,
            HistoryId = mutation.HistoryId,
            Payload = payload?.ToString(Formatting.None)
        });
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return state.Version;
    }

    public async Task<SyncBatch> Read(int accountId, long cursor, CancellationToken token,
        bool languagePacks = false, bool languagePackSnapshot = false, bool uiLanguage = false)
    {
        if (cursor < 0) throw new ArgumentException("Invalid sync cursor.");
        await using var transaction = await database.Database.BeginTransactionAsync(MySql ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, token);
        var state = await LockAccount(accountId, token);
        if (cursor > state.Version) throw new SyncConflictException("Sync cursor is ahead of the server.");
        if (cursor == 0)
        {
            // Bootstrap existing installations without inventing history for records predating the change log.
            var histories = await database.ChatHistories!.AsNoTracking().Where(item => item.UserId == accountId).ToListAsync(token);
            var messages = await database.ChatMessages!.AsNoTracking().Include(item => item.FileList)
                .Where(item => item.ChatHistory!.UserId == accountId).ToListAsync(token);
            var historyIds = histories.ToDictionary(item => item.ChatHistoryId, item => item.DataId);
            var tasks = await database.GenerateTasks!.AsNoTracking().Where(item => item.AccountId == accountId).ToListAsync(token);
            var changes = histories.Select(item => new SyncEvent(state.Version, "history", "upsert", item.DataId.ToString(), null, HistoryJson(item)))
                .Concat(messages.Select(item => new SyncEvent(state.Version, "message", "upsert", item.DataId.ToString(), historyIds[item.ChatHistoryId], MessageJson(item))))
                .Concat(tasks.Select(item => new SyncEvent(state.Version, "task", "upsert", item.TaskId, null, TaskJson(item)))).ToList();
            var packs = languagePacks ? await ReadLanguagePacks(accountId, uiLanguage, token) : null;
            await transaction.CommitAsync(token);
            return new(state.Version, true, false, changes, packs);
        }
        var page = await database.Set<SyncChange>().AsNoTracking().Where(item => item.AccountId == accountId && item.Version > cursor)
            .OrderBy(item => item.Version).Take(200).ToListAsync(token);
        var next = page.Count == 0 ? cursor : page[^1].Version;
        var hasMore = next < state.Version;
        var snapshot = languagePacks && languagePackSnapshot && !hasMore ? await ReadLanguagePacks(accountId, uiLanguage, token) : null;
        await transaction.CommitAsync(token);
        return new(next, false, hasMore, page
            .Where(item => item.Kind switch
            {
                "languagePack" => languagePacks && !languagePackSnapshot,
                "uiLanguage" => languagePacks && uiLanguage && !languagePackSnapshot,
                _ => true
            }).Select(ToEvent).ToList(), snapshot);
    }

    private async Task<IReadOnlyList<SyncEvent>> ReadLanguagePacks(int accountId, bool uiLanguage, CancellationToken token)
    {
        // The latest event per code is the current value, including deletions.
        var events = database.Set<SyncChange>().AsNoTracking()
            .Where(item => item.AccountId == accountId && (item.Kind == "languagePack" || (uiLanguage && item.Kind == "uiLanguage")));
        var versions = events.GroupBy(item => new { item.Kind, item.EntityId }).Select(group => group.Max(item => item.Version));
        var packs = await events.Where(item => versions.Contains(item.Version) && item.Action == "upsert").ToListAsync(token);
        return packs.Select(ToEvent).ToList();
    }

    private static SyncEvent ToEvent(SyncChange item) => new(item.Version, item.Kind, item.Action,
        item.EntityId, item.HistoryId, item.Payload is null ? null : JToken.Parse(item.Payload));

    private async Task<JToken> UiLanguage(int accountId, SyncMutation mutation, CancellationToken token)
    {
        if (mutation.EntityId != "selected" || mutation.Action != "upsert" || mutation.HistoryId is not null ||
            mutation.Payload is not JObject payload || payload["code"]?.Type != JTokenType.String ||
            (string?)payload["code"] is not { Length: <= 64 } code ||
            !Regex.IsMatch(code, @"\A[a-z]{2,8}(?:-[a-z0-9]{1,8})*\z") ||
            (payload["initialize"] is { } initialize && initialize.Type != JTokenType.Boolean))
            throw new ArgumentException("Invalid interface language.");

        if ((bool?)payload["initialize"] == true)
        {
            var existing = await database.Set<SyncChange>().Where(item => item.AccountId == accountId && item.Kind == "uiLanguage")
                .OrderByDescending(item => item.Version).Select(item => item.Payload).FirstOrDefaultAsync(token);
            if (existing is not null) return JToken.Parse(existing);
        }
        return new JObject { ["code"] = code };
    }

    private static JToken? LanguagePack(SyncMutation mutation)
    {
        var code = mutation.EntityId;
        if (mutation.HistoryId is not null || code.Length > 64 || code is "zh" or "en" or "ja" or "jp" ||
            !Regex.IsMatch(code, @"\A[a-z]{2,8}(?:-[a-z0-9]{1,8})*\z"))
            throw new ArgumentException("Invalid language code.");
        if (mutation.Action == "delete") return null;
        if (mutation.Payload is not JObject payload || payload["document"]?.Type != JTokenType.String ||
            (string?)payload["document"] is not { } document || Encoding.UTF8.GetByteCount(document) > 256 * 1024)
            throw new ArgumentException("Invalid language pack.");
        JObject pack;
        try
        {
            using var reader = new JsonTextReader(new StringReader(document)) { DateParseHandling = DateParseHandling.None, MaxDepth = 16 };
            pack = JObject.Load(reader);
            if (reader.Read()) throw new ArgumentException("Invalid language pack.");
        }
        catch (JsonException exception) { throw new ArgumentException("Invalid language pack.", exception); }
        if (pack["code"]?.Type != JTokenType.String ||
            (string?)pack["code"] != code || pack["name"]?.Type != JTokenType.String ||
            (string?)pack["name"] is not { Length: > 0 and <= 80 } name || !HasText(name) ||
            pack["translations"] is not JObject dictionary || !ValidDictionary(dictionary, 0))
            throw new ArgumentException("Invalid language pack.");
        return new JObject { ["document"] = document };
    }

    private static bool ValidDictionary(JObject dictionary, int depth) =>
        depth <= 12 && dictionary.Count > 0 && dictionary.Properties().All(property =>
            Regex.IsMatch(property.Name, @"\A[A-Za-z0-9_-]+\z") &&
            property.Name is not ("__proto__" or "constructor" or "prototype" or "hasOwnProperty" or
                "isPrototypeOf" or "propertyIsEnumerable" or "toLocaleString" or "toString" or "valueOf" or
                "__defineGetter__" or "__defineSetter__" or "__lookupGetter__" or "__lookupSetter__") &&
            (property.Value is JObject child ? ValidDictionary(child, depth + 1) :
                property.Value.Type == JTokenType.String && HasText((string)property.Value!)));

    private static bool HasText(string value) =>
        value.Any(character => character != '\uFEFF' && (!char.IsWhiteSpace(character) || character == '\u0085'));

    private static long NumericId(SyncMutation mutation) => long.TryParse(mutation.EntityId, out var id) && id > 0 && id <= 9007199254740991
        ? id : throw new ArgumentException("Invalid client ID.");
    private static T Payload<T>(SyncMutation mutation) where T : class
    {
        if (mutation.Payload is not JObject) throw new ArgumentException("Missing or invalid sync payload.");
        try
        {
            var payload = mutation.Payload.ToObject<T>() ?? throw new ArgumentException("Missing sync payload.");
            if (payload is MMessage message && message.FileList?.Any(file => file is null) == true)
                throw new ArgumentException("File list must not contain null entries.");
            return payload;
        }
        catch (JsonException exception) { throw new ArgumentException("Invalid sync payload.", exception); }
    }

    private async Task<JToken?> History(int accountId, SyncMutation mutation, CancellationToken token)
    {
        var id = NumericId(mutation);
        var history = await database.ChatHistories!.SingleOrDefaultAsync(item => item.UserId == accountId && item.DataId == id, token);
        if (mutation.Action == "delete") { if (history is not null) database.Remove(history); return null; }
        if (history is null) { history = new ChatHistory { UserId = accountId, DataId = id }; database.Add(history); }
        history.Title = Payload<ChatHistory>(mutation).Title;
        return HistoryJson(history);
    }

    private async Task<JToken?> Message(int accountId, SyncMutation mutation, CancellationToken token)
    {
        var id = NumericId(mutation);
        var history = await database.ChatHistories!.SingleOrDefaultAsync(item => item.UserId == accountId && item.DataId == mutation.HistoryId, token);
        if (history is null)
        {
            if (mutation.Action == "delete") return null;
            throw new SyncConflictException("Create the history before its messages.");
        }
        var message = await database.ChatMessages!.Include(item => item.FileList)
            .SingleOrDefaultAsync(item => item.ChatHistoryId == history.ChatHistoryId && item.DataId == id, token);
        if (mutation.Action == "delete") { if (message is not null) database.Remove(message); return null; }
        var incoming = Payload<MMessage>(mutation);
        if (message is null) { message = new ChatMessage { DataId = id, ChatHistory = history }; database.Add(message); }
        else database.RemoveRange(message.FileList ?? []);
        message.Role = incoming.Role;
        message.Content = incoming.Content;
        message.Model = incoming.Model;
        message.Finish = incoming.Finish;
        message.ShowType = incoming.ShowType;
        message.FileList = (incoming.FileList ?? []).Select(file => new FileAdds
        {
            FileName = file.FileName,
            FileType = file.FileType,
            FileSize = file.FileSize,
            FileContent = file.FileContent,
            ParsedContent = file.ParsedContent
        }).ToList();
        return MessageJson(message);
    }

    private async Task<JToken?> Task(int accountId, SyncMutation mutation, CancellationToken token)
    {
        var task = await database.GenerateTasks!.SingleOrDefaultAsync(item => item.TaskId == mutation.EntityId, token);
        if (task is not null && task.AccountId != accountId) throw new KeyNotFoundException("Task not found.");
        if (mutation.Action == "delete") { if (task is not null) database.Remove(task); return null; }
        var incoming = Payload<GenerateTask>(mutation);
        if (task is null) { task = new GenerateTask { TaskId = mutation.EntityId, AccountId = accountId }; database.Add(task); }
        task.TaskType = incoming.TaskType; task.DateTime = incoming.DateTime;
        task.Images = incoming.Images; task.Videos = incoming.Videos;
        return TaskJson(task);
    }

    private static JToken HistoryJson(ChatHistory item) => Json(new { item.DataId, item.Title, item.UserId });
    private static JToken MessageJson(ChatMessage item) => Json(new
    {
        item.DataId,
        item.Role,
        item.Content,
        item.Model,
        item.Finish,
        item.ShowType,
        FileList = item.FileList?.Select(file => new { file.FileName, file.FileType, file.FileSize, file.FileContent, file.ParsedContent })
    });
    private static JToken TaskJson(GenerateTask item) => Json(new
    {
        task_id = item.TaskId,
        task_type = item.TaskType,
        date = item.DateTime,
        account_id = item.AccountId,
        images = item.Images,
        videos = item.Videos
    });
}
