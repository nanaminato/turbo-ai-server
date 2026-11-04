using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Turbo.Auth.Data.Contexts;
using Turbo.Auth.Models.Accounts;
using Turbo.Auth.Models.Sync.Messages;
using Turbo.Auth.Repositories.Sync;

namespace Turbo.Auth.Tests;

public class SyncRepositoryTests
{
    private SqliteConnection _connection = null!;
    private AuthContext _database = null!;
    private AuthContext Open() => new(new DbContextOptionsBuilder<AuthContext>().UseSqlite(_connection).Options,
        new ConfigurationBuilder().Build());

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _connection.CreateCollation("utf8mb4_0900_ai_ci", StringComparer.OrdinalIgnoreCase.Compare);
        _database = Open();
        await _database.Database.EnsureCreatedAsync();
        _database.Accounts!.AddRange(Enumerable.Range(1, 2).Select(id => new Account
            { AccountId = id, Username = $"user{id}", Password = "hash", Email = $"user{id}@example.com" }));
        _database.ChatHistories!.AddRange(
            new ChatHistory { ChatHistoryId = 1, UserId = 1, DataId = 100, Title = "one" },
            new ChatHistory { ChatHistoryId = 2, UserId = 2, DataId = 100, Title = "two" },
            new ChatHistory { ChatHistoryId = 3, UserId = 1, DataId = 101, Title = "another" });
        _database.ChatMessages!.AddRange(Enumerable.Range(1, 3).Select(id => new ChatMessage
        {
            ChatHistoryId = id, DataId = 200, Role = "user", Content = $"message{id}",
            FileList = [new FileAdds { FileName = $"file{id}", FileContent = "original" }]
        }));
        await _database.SaveChangesAsync();
        _database.ChangeTracker.Clear();
    }

    [TearDown]
    public async Task TearDown() { await _database.DisposeAsync(); await _connection.DisposeAsync(); }

    [Test]
    public async Task Duplicate_client_ids_stay_inside_the_account_and_history()
    {
        var histories = new HistoryRepository(_database);
        Assert.That((await histories.GetHistories(2, [])).Select(item => item.Title), Is.EqualTo(new[] { "two" }));
        Assert.That(await histories.GetHistories(2, [100]), Is.Empty);
        var messages = new MessageRepository(_database, histories);
        Assert.That((await messages.GetMessages(2, 100, [])).Single().Content, Is.EqualTo("message2"));
        Assert.That((await messages.GetMessages(1, 101, [])).Single().FileList!.Single().FileName, Is.EqualTo("file3"));
        Assert.That(await messages.GetMessages(2, 101, []), Is.Empty);
        Assert.That(await messages.GetMessages(2, 100, [200]), Is.Empty);
    }

    [Test]
    public async Task Updating_a_message_keeps_identity_and_replaces_attachments()
    {
        var id = await _database.ChatMessages!.Where(item => item.ChatHistoryId == 1).Select(item => item.ChatMessageId).SingleAsync();
        await new Turbo.Auth.Application.Sync.DataSyncService(_database).Apply(1, new(Guid.NewGuid(), "message", "upsert", "200", 100, Newtonsoft.Json.Linq.JObject.FromObject(new MMessage
        {
            UserId = 1, HistoryDataId = 100, DataId = 200, Content = "updated",
            FileList = [new FileAdds { FileName = "new", FileAddsId = 999, ChatMessageId = 999 }]
        })), default);
        _database.ChangeTracker.Clear();
        var updated = await _database.ChatMessages!.Include(item => item.FileList).SingleAsync(item => item.ChatMessageId == id);
        Assert.That(updated.Content, Is.EqualTo("updated"));
        Assert.That(updated.FileList!.Single().FileName, Is.EqualTo("new"));
        Assert.That(updated.FileList!.Single().FileAddsId, Is.Not.EqualTo(999));
    }
    [Test]
    public async Task Sync_cursor_replays_updates_deletes_and_idempotent_operations()
    {
        var sync = new Turbo.Auth.Application.Sync.DataSyncService(_database);
        var history = new Turbo.Auth.Application.Sync.SyncMutation(Guid.NewGuid(), "history", "upsert", "100",
            Payload: Newtonsoft.Json.Linq.JObject.FromObject(new { title = "renamed" }));
        Assert.That(await sync.Apply(1, history, default), Is.EqualTo(1));
        var bootstrap = await sync.Read(1, 0, default);
        Assert.That(bootstrap.Reset, Is.True);
        Assert.That(bootstrap.Cursor, Is.EqualTo(1));
        Assert.That(bootstrap.Changes.Count(item => item.Kind == "history"), Is.EqualTo(2));
        var message = new Turbo.Auth.Application.Sync.SyncMutation(Guid.NewGuid(), "message", "upsert", "200", 100,
            Newtonsoft.Json.Linq.JObject.FromObject(new { role = "assistant", content = "updated", finish = true }));
        Assert.That(await sync.Apply(1, message, default), Is.EqualTo(2));
        Assert.That(await sync.Apply(1, message, default), Is.EqualTo(2));
        var changes = await sync.Read(1, 1, default);
        Assert.That(changes.Changes.Single().Payload!["content"]!.ToString(), Is.EqualTo("updated"));
        await sync.Apply(1, new(Guid.NewGuid(), "message", "delete", "200", 100), default);
        var deletion = (await sync.Read(1, 2, default)).Changes.Single();
        Assert.That(deletion.Action, Is.EqualTo("delete"));
        Assert.That(deletion.HistoryId, Is.EqualTo(100));
        Assert.That((await sync.Read(2, 0, default)).Changes.Single(item => item.Kind == "message").Payload!["content"]!.ToString(), Is.EqualTo("message2"));
        Assert.ThrowsAsync<Turbo.Auth.Application.Sync.SyncConflictException>(() => sync.Apply(1,
            message with { Action = "delete", Payload = null }, default));
    }

    [Test]
    public async Task Reused_scope_refreshes_the_version_after_another_writer_commits()
    {
        var sync = new Turbo.Auth.Application.Sync.DataSyncService(_database);
        await sync.Read(1, 0, default);
        await using (var other = Open())
        {
            await new Turbo.Auth.Application.Sync.DataSyncService(other).Apply(1,
                new(Guid.NewGuid(), "history", "upsert", "100", Payload: Newtonsoft.Json.Linq.JObject.FromObject(new {title = "other writer"})), default);
        }
        var version = await sync.Apply(1,
            new(Guid.NewGuid(), "history", "upsert", "100", Payload: Newtonsoft.Json.Linq.JObject.FromObject(new {title = "reused scope"})), default);
        Assert.That(version, Is.EqualTo(2));
        Assert.That((await sync.Read(1, 1, default)).Changes.Single().Version, Is.EqualTo(2));
    }

    [Test]
    public async Task Sync_rollback_does_not_advance_cursor_or_lose_attachments()
    {
        await _database.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_sync_attachment BEFORE INSERT ON FileAdds
            WHEN NEW.FileName = 'reject' BEGIN SELECT RAISE(ABORT, 'attachment rejected'); END;
            """);
        var sync = new Turbo.Auth.Application.Sync.DataSyncService(_database);
        Assert.ThrowsAsync<DbUpdateException>(() => sync.Apply(1, new(Guid.NewGuid(), "message", "upsert", "200", 100,
            Newtonsoft.Json.Linq.JObject.FromObject(new { content = "bad", fileList = new[] { new {fileName = "reject"} } })), default));
        await using var verification = Open();
        Assert.That(await verification.Set<Turbo.Auth.Application.Sync.SyncChange>().CountAsync(), Is.Zero);
        var preserved = await verification.ChatMessages!.Include(item => item.FileList).SingleAsync(item => item.ChatHistoryId == 1);
        Assert.That(preserved.Content, Is.EqualTo("message1"));
        Assert.That(preserved.FileList!.Single().FileName, Is.EqualTo("file1"));
    }

}
