using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Turbo.Auth.Data.Contexts;
using Turbo.Auth.Models.Accounts;
using Turbo.Auth.Models.Sync.Messages;
using Turbo.Auth.Models.Sync.Tasks;
using Turbo.Auth.Models.Suppliers;
using Turbo.Auth.Repositories.Catalog;
using Turbo.Auth.Repositories.Accounts;
using Turbo.Auth.Repositories.Sync;
using Turbo.Auth.Security;

namespace Turbo.Auth.Tests;

public class RuntimeQueryTests
{
    private sealed class Queries : DbCommandInterceptor
    {
        public readonly List<string> Commands = [];
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
    private SqliteConnection connection = null!;
    private AuthContext db = null!;
    private Queries queries = null!;

    [SetUp]
    public async Task Setup()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation("utf8mb4_0900_ai_ci", StringComparer.OrdinalIgnoreCase.Compare);
        queries = new Queries();
        db = new AuthContext(new DbContextOptionsBuilder<AuthContext>().UseSqlite(connection).AddInterceptors(queries).Options,
            new ConfigurationBuilder().Build());
        await db.Database.EnsureCreatedAsync();
        db.Accounts!.AddRange(Enumerable.Range(1, 2).Select(id => new Account
            { AccountId = id, Username = $"user{id}", Password = "private-password-hash", Email = $"user{id}@example.com" }));
        db.Roles!.AddRange(new Role { RoleId = 1, Code = "user", Name = "User" }, new Role { RoleId = 2, Code = "editor", Name = "Editor" });
        db.AccountRoles!.AddRange(new AccountRole { AccountId = 1, RoleId = 1 }, new AccountRole { AccountId = 1, RoleId = 2 },
            new AccountRole { AccountId = 2, RoleId = 1 });
        db.ChatHistories!.AddRange(Enumerable.Range(1, 2).Select(id => new ChatHistory
            { ChatHistoryId = id, UserId = id, DataId = 100, Title = $"history{id}" }));
        db.ChatMessages!.AddRange(Enumerable.Range(1, 2).Select(id => new ChatMessage
        {
            ChatHistoryId = id, DataId = 200, Content = $"message{id}",
            FileList = [new FileAdds { FileName = $"file{id}", FileContent = $"raw{id}", ParsedContent = $"parsed{id}" }]
        }));
        var date = new DateTime(2026, 11, 5, 1, 0, 0, DateTimeKind.Utc);
        db.GenerateTasks!.AddRange(
            new GenerateTask { TaskId = "b", AccountId = 1, DateTime = date, Images = ["one", "two"], Videos = ["v1", "v2"] },
            new GenerateTask { TaskId = "a", AccountId = 1, DateTime = date },
            new GenerateTask { TaskId = "earlier", AccountId = 1, DateTime = date.AddDays(-1) },
            new GenerateTask { TaskId = "other", AccountId = 2, DateTime = date });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear(); queries.Commands.Clear();
    }

    [TearDown]
    public async Task Cleanup() { await db.DisposeAsync(); await connection.DisposeAsync(); }

    [Test]
    public async Task Account_projection_omits_secrets_and_role_filter_keeps_all_assigned_roles()
    {
        var repository = new AccountRepository(db, new AccountPasswordService(), RoleSafetyTests.Sessions(db));
        var accounts = await repository.GetAccountsWithRoleAsync(2);
        Assert.That(accounts.Select(account => account.AccountId), Is.EqualTo(new[] { 1 }));
        Assert.That(accounts.Single().UserRoles.Select(role => role.Code), Is.EquivalentTo(new[] { "user", "editor" }));
        Assert.That(queries.Commands, Is.Not.Empty);
        Assert.That(queries.Commands.All(sql => !sql.Contains("Password", StringComparison.OrdinalIgnoreCase) &&
            !sql.Contains("SecurityStamp", StringComparison.OrdinalIgnoreCase)), Is.True);
        Assert.That(db.ChangeTracker.Entries(), Is.Empty);
    }

    [Test]
    public async Task Message_query_is_single_tenant_scoped_select_and_keeps_attachment_payload()
    {
        var repository = new MessageRepository(db, new HistoryRepository(db));
        var message = (await repository.GetMessages(2, 100, [])).Single();
        Assert.That(queries.Commands.Count, Is.EqualTo(1));
        Assert.That(message.Content, Is.EqualTo("message2"));
        Assert.That(message.FileList!.Single().FileContent, Is.EqualTo("raw2"));
        Assert.That(message.FileList!.Single().ParsedContent, Is.EqualTo("parsed2"));
        Assert.That(await repository.GetMessages(2, 100, [200]), Is.Empty);
        Assert.That(await repository.GetMessages(2, 999, []), Is.Empty);
        Assert.That(db.ChangeTracker.Entries(), Is.Empty);
    }

    [Test]
    public async Task Tasks_are_untracked_tenant_scoped_and_stably_ordered_for_equal_dates()
    {
        var repository = new TaskRepository(db);
        Assert.That((await repository.GetGenerateTasks(1, null)).Select(task => task.TaskId), Is.EqualTo(new[] { "earlier", "a", "b" }));
        Assert.That((await repository.GetGenerateTasks(1, ["a"])).Select(task => task.TaskId), Is.EqualTo(new[] { "earlier", "b" }));
        Assert.That(db.ChangeTracker.Entries(), Is.Empty);
    }

    [Test, Explicit("Requires isolated MySQL database")]
    public async Task Optimized_projections_and_tenant_queries_execute_on_mysql()
    {
        await using var fixture = await MySqlTestDatabase.Create();
        await fixture.Execute("""
            INSERT INTO Roles(RoleId,Code,Name,Description) VALUES
                (1,'user','User','System user'),(2,'editor','Display editor','Custom description');
            INSERT INTO AccountRoles(AccountId,RoleId) VALUES(1,1),(1,2),(2,1);
            INSERT INTO GenerateTasks(TaskId,TaskType,AccountId,DateTime,Images,Videos) VALUES
                ('b','image',1,'2026-11-05 00:00:00',JSON_ARRAY('b.png'),JSON_ARRAY()),
                ('a','image',1,'2026-11-05 00:00:00',JSON_ARRAY('a.png'),JSON_ARRAY()),
                ('other','image',2,'2026-11-05 00:00:00',JSON_ARRAY(),JSON_ARRAY());
            """);
        await using var context = fixture.Auth();
        var accounts = new AccountRepository(context, new AccountPasswordService(), RoleSafetyTests.Sessions(context));
        var editor = (await accounts.GetAccountsWithRoleAsync(2)).Single();
        Assert.That(editor.AccountId, Is.EqualTo(1));
        Assert.That(editor.UserRoles.Select(role => role.Code), Is.EquivalentTo(new[] { "user", "editor" }));
        Assert.That(editor.UserRoles.Single(role => role.Code == "editor").Description, Is.EqualTo("Custom description"));
        Assert.That(editor.UserRoles.Single(role => role.Code == "user").IsSystem, Is.True);
        Assert.That((await accounts.GetAccountByIdAsync(2)).Username, Is.EqualTo("test-two"));
        Assert.That((await accounts.GetAccountsAsync()).Count, Is.EqualTo(2));
        var messages = new MessageRepository(context, new HistoryRepository(context));
        var message = (await messages.GetMessages(1, 100, [])).Single();
        Assert.That(message.Content, Is.EqualTo("legacy message"));
        Assert.That(message.FileList!.Single().FileContent, Is.EqualTo("test"));
        Assert.That((await messages.GetMessages(2, 100, [])).Single().Content, Is.EqualTo("private message"));
        Assert.That(await messages.GetMessages(1, 100, [200]), Is.Empty);
        Assert.That((await new TaskRepository(context).GetGenerateTasks(1, ["legacy-task"])).Select(task => task.TaskId),
            Is.EqualTo(new[] { "a", "b" }));
        Assert.That(context.ChangeTracker.Entries(), Is.Empty);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Json_media_in_place_add_remove_reorder_and_null_persist_without_phantom_changes(bool images)
    {
        GenerateTask task = (await db.GenerateTasks!.FindAsync("b"))!;
        List<string>? Get() => images ? task.Images : task.Videos;
        void Set(List<string>? value) { if (images) task.Images = value; else task.Videos = value; }
        async Task PersistAndReload(string[]? expected)
        {
            Assert.That(await db.SaveChangesAsync(), Is.EqualTo(1));
            db.ChangeTracker.Clear(); task = (await db.GenerateTasks!.FindAsync("b"))!;
            Assert.That(Get(), Is.EqualTo(expected));
            Assert.That(db.ChangeTracker.HasChanges(), Is.False);
            Assert.That(await db.SaveChangesAsync(), Is.Zero);
        }
        var original = Get()!.ToArray();
        Get()!.Add("added"); await PersistAndReload([..original, "added"]);
        Get()!.RemoveAt(0); await PersistAndReload([original[1], "added"]);
        Get()!.Reverse(); await PersistAndReload(["added", original[1]]);
        Set(null); await PersistAndReload(null);
        Set([]); await PersistAndReload([]);
    }

    [Test]
    public async Task Key_model_filter_uses_one_query_and_preserves_the_full_untracked_binding_graph()
    {
        await using var keyDb = await SeedKeys();
        queries.Commands.Clear();
        var keys = await new KeyRepository(keyDb).GetKeysWithModelAsync(1);
        Assert.That(queries.Commands.Count, Is.EqualTo(1));
        Assert.That(keys!.Select(key => key.SupplierKeyId), Is.EqualTo(new[] { 1 }));
        Assert.That(keys!.Single().ModelKeyBinds!.Select(binding => binding.ModelId), Is.EquivalentTo(new[] { 1, 2 }));
        Assert.That(keys!.Single().ModelKeyBinds!.All(binding => binding.Model?.Name is not null), Is.True);
        Assert.That(keyDb.ChangeTracker.Entries(), Is.Empty);
    }

    [Test]
    public async Task Model_enable_update_is_one_scoped_statement_and_other_model_bindings_are_untouched()
    {
        await using var keyDb = await SeedKeys();
        queries.Commands.Clear();
        await new ModelRepository(keyDb).SetEnableStatus(2, false);
        Assert.That(queries.Commands.Count, Is.EqualTo(1));
        Assert.That(queries.Commands.Single().TrimStart(), Does.StartWith("UPDATE"));
        Assert.That(keyDb.ChangeTracker.Entries(), Is.Empty);
        var bindings = await keyDb.ModelKeyBinds!.AsNoTracking().OrderBy(binding => binding.ModelKeyBindId).ToListAsync();
        Assert.That(bindings.Where(binding => binding.ModelId == 2).All(binding => !binding.Enable), Is.True);
        Assert.That(bindings.Single(binding => binding.ModelId == 1).Enable, Is.True);
    }

    private async Task<KeyContext> SeedKeys()
    {
        // AuthContext and KeyContext map different tables, so give this context a separate database.
        var keyDb = new KeyContext(new DbContextOptionsBuilder<KeyContext>().UseSqlite("Data Source=:memory:")
            .AddInterceptors(queries).Options, new ConfigurationBuilder().Build());
        await keyDb.Database.OpenConnectionAsync();
        ((SqliteConnection)keyDb.Database.GetDbConnection()).CreateCollation("utf8mb4_0900_ai_ci", StringComparer.OrdinalIgnoreCase.Compare);
        await keyDb.Database.EnsureCreatedAsync();
        keyDb.Models!.AddRange(new Model { ModelId = 1, Name = "One", ModelValue = "one" }, new Model { ModelId = 2, Name = "Two", ModelValue = "two" });
        keyDb.SupplierKeys!.AddRange(Enumerable.Range(1, 2).Select(id => new SupplierKey
            { SupplierKeyId = id, ApiKey = "test-key", BaseUrl = "https://example.invalid", Enable = true }));
        keyDb.ModelKeyBinds!.AddRange(new ModelKeyBind { SupplierKeyId = 1, ModelId = 1, Enable = true },
            new ModelKeyBind { SupplierKeyId = 1, ModelId = 2, Enable = true }, new ModelKeyBind { SupplierKeyId = 2, ModelId = 2, Enable = true });
        await keyDb.SaveChangesAsync(); keyDb.ChangeTracker.Clear();
        return keyDb;
    }
}
