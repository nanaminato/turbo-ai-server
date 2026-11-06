using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using RelaxKon.Auth.Application.Routing;
using RelaxKon.Auth.Application.Sync;
using RelaxKon.Auth.Bootstrap;
using RelaxKon.Auth.Contracts.Auth;
using RelaxKon.Auth.Data;
using RelaxKon.Auth.Data.Contexts;
using RelaxKon.Auth.Models.Accounts;
using RelaxKon.Auth.Models.Config;
using RelaxKon.Auth.Models.Suppliers;
using RelaxKon.Auth.Models.Sync.Messages;
using RelaxKon.Auth.Models.Sync.Tasks;
using RelaxKon.Auth.Repositories.Accounts;
using RelaxKon.Auth.Repositories.Sync;
using RelaxKon.Auth.Security;
using RelaxKon.Auth.Security.FeatureAccess;
using RelaxKon.Auth.Security.PasswordReset;
using RelaxKon.Auth.Security.Registration;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace RelaxKon.Auth.Tests;

// Unlike the in-memory unit fixtures, these exercise the production schema and
// independently opened connections contending on one real, temporary local file.
[NonParallelizable]
public sealed class SQLitePersistenceTests
{
    private string directory = null!;
    private IConfiguration configuration = null!;
    private MemoryDistributedCache cache = null!;
    private int adminId;
    private readonly JwtSettings jwt = new() { RefreshTokenPepper = "sqlite-tests-only-pepper" };

    [SetUp]
    public async Task Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "relaxkon-sqlite-persistence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:ciko"] = new SqliteConnectionStringBuilder
                { DataSource = Path.Combine(directory, "test.db"), Pooling = false }.ConnectionString
        }).Build();
        cache = new MemoryDistributedCache(OptionsFactory.Create(new MemoryDistributedCacheOptions()));
        adminId = await new DatabaseBootstrapper().RunAsync(DatabaseProvider.FromConfiguration(configuration),
            new BootstrapAdmin("admin", "admin@example.invalid", "Password123!"), initialize: true);
    }

    [TearDown]
    public void Cleanup()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private AuthContext Auth() => new(configuration);
    private KeyContext Keys() => new(configuration);
    private AuthSessionService Sessions(AuthContext context) => new(context, cache, OptionsFactory.Create(jwt));

    private static async Task<T[]> Race<T>(int count, Func<int, Task<T>> action)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, count).Select(index => Task.Run(async () =>
        {
            await start.Task;
            return await action(index);
        })).ToArray();
        start.SetResult();
        return await Task.WhenAll(tasks);
    }

    [Test]
    public async Task Concurrent_sync_writes_have_contiguous_versions_and_retries_are_idempotent()
    {
        var mutations = Enumerable.Range(0, 8).Select(index => new SyncMutation(Guid.NewGuid(), "history", "upsert",
            (100 + index).ToString(), Payload: JObject.FromObject(new { title = "history " + index }))).ToArray();
        async Task<long> Apply(SyncMutation mutation)
        {
            await using var context = Auth();
            return await new DataSyncService(context).Apply(adminId, mutation, default);
        }
        var versions = await Race(mutations.Length, index => Apply(mutations[index]));
        Assert.That(versions.Order(), Is.EqualTo(Enumerable.Range(1, 8).Select(value => (long)value)));
        var retried = await Race(6, _ => Apply(mutations[0]));
        Assert.That(retried.Distinct(), Is.EqualTo(new[] { versions[0] }));
        await using var check = Auth();
        Assert.That(await check.Set<SyncChange>().CountAsync(), Is.EqualTo(8));
        Assert.That(await check.ChatHistories!.CountAsync(), Is.EqualTo(8));
        var reader = new DataSyncService(check);
        var batch = await reader.Read(adminId, 1, default);
        Assert.That(batch.Cursor, Is.EqualTo(8));
        Assert.That(batch.Changes.Select(change => change.Version), Is.EqualTo(Enumerable.Range(2, 7).Select(value => (long)value)));
        Assert.ThrowsAsync<SyncConflictException>(() => reader.Apply(adminId,
            mutations[0] with { Payload = JObject.FromObject(new { title = "different" }) }, default));
    }

    [Test]
    public async Task Concurrent_admin_demotions_leave_one_admin()
    {
        int otherId;
        int userRole;
        await using (var seed = Auth())
        {
            var adminRole = await seed.Roles!.Where(role => role.Code == "admin").Select(role => role.RoleId).SingleAsync();
            userRole = await seed.Roles!.Where(role => role.Code == "user").Select(role => role.RoleId).SingleAsync();
            var other = new Account { Username = "other", Email = "other@example.invalid", Password = "hash" };
            seed.Accounts!.Add(other);
            seed.AccountRoles!.Add(new AccountRole { Account = other, RoleId = adminRole });
            await seed.SaveChangesAsync();
            otherId = other.AccountId;
        }
        var ids = new[] { adminId, otherId };
        var outcomes = await Race(2, async index =>
        {
            await using var context = Auth();
            try
            {
                await new AccountRepository(context, new AccountPasswordService(), Sessions(context)).UpdateAccountAsync(
                    new AccountWriteRequest { AccountId = ids[index], Username = "demoted" + index,
                        Email = $"demoted{index}@example.invalid", UserRoles = [new RoleReferenceRequest { RoleId = userRole }] });
                return true;
            }
            catch (RoleRuleException error) when (error.Code == "last_admin_required") { return false; }
        });
        Assert.That(outcomes.Count(value => value), Is.EqualTo(1));
        await using var check = Auth();
        Assert.That(await check.AccountRoles!.CountAsync(link => link.Role!.Code == "admin"), Is.EqualTo(1));
    }

    [Test]
    public async Task Concurrent_registration_enforces_cooldown_single_use_and_case_insensitive_names()
    {
        var sender = new RegistrationSender();
        var sends = await Race(8, async _ =>
        {
            await using var context = Auth();
            return await Registration(context, sender).SendCode(" Shared@Example.Invalid ", default);
        });
        Assert.That(sends.Count(result => result.Status == EmailCodeStatus.Sent), Is.EqualTo(1));
        Assert.That(sender.Calls, Is.EqualTo(1));
        var request = Request("shared@example.invalid", "Shared", sender.Codes["shared@example.invalid"]);
        var registrations = await Race(8, async _ =>
        {
            await using var context = Auth();
            return await Registration(context, sender).Register(request, default);
        });
        Assert.That(registrations.Count(result => result == RegistrationStatus.Success), Is.EqualTo(1));
        Assert.That(registrations.Count(result => result == RegistrationStatus.InvalidCode), Is.EqualTo(7));

        var emails = new[] { "first@example.invalid", "second@example.invalid" };
        foreach (var email in emails)
        {
            await using var context = Auth();
            await Registration(context, sender).SendCode(email, default);
        }
        var names = await Race(2, async index =>
        {
            await using var context = Auth();
            return await Registration(context, sender).Register(Request(emails[index], index == 0 ? "CaseName" : "casename",
                sender.Codes[emails[index]]), default);
        });
        Assert.That(names.Count(result => result == RegistrationStatus.Success), Is.EqualTo(1));
        Assert.That(names.Count(result => result == RegistrationStatus.AccountExists), Is.EqualTo(1));
        await using var check = Auth();
        Assert.That(await check.Accounts!.CountAsync(account => account.Username == "CASENAME"), Is.EqualTo(1));
        Assert.That(await check.AccountRoles!.CountAsync(link => link.AccountId != adminId && link.Role!.Code == "user"), Is.EqualTo(2));
    }

    [Test]
    public async Task Concurrent_refresh_rotation_detects_replay_and_revokes_the_replacement()
    {
        RefreshTokenIssue issue;
        string previousStamp;
        await using (var context = Auth())
        {
            var account = await context.Accounts!.SingleAsync(account => account.AccountId == adminId);
            previousStamp = account.SecurityStamp;
            issue = await Sessions(context).CreateSessionAsync(account, "test-device", null);
        }
        var results = await Race(2, async _ =>
        {
            await using var context = Auth();
            return await Sessions(context).RotateAsync(issue.RefreshToken, "test-device", null);
        });
        Assert.That(results.Count(result => result.Succeeded), Is.EqualTo(1));
        Assert.That(results.Count(result => result.IsReplay), Is.EqualTo(1));
        await using var check = Auth();
        Assert.That(await check.RefreshTokens!.CountAsync(), Is.EqualTo(2));
        Assert.That(await check.RefreshTokens!.AllAsync(token => token.RevokedAt != null), Is.True);
        Assert.That((await check.Accounts!.SingleAsync()).SecurityStamp, Is.Not.EqualTo(previousStamp));
        Assert.That((await Sessions(check).GetActiveSessionsAsync(adminId)), Is.Empty);
    }

    [Test]
    public async Task Concurrent_password_reset_is_single_use_and_revokes_sessions()
    {
        ResetIdentity identity;
        await using (var context = Auth())
        {
            var account = await context.Accounts!.SingleAsync();
            identity = new(account.AccountId, account.Email!, account.SecurityStamp, ResetTokenService.Fingerprint(account.Password));
            await Sessions(context).CreateSessionAsync(account, "test-device", null);
        }
        var results = await Race(2, async index =>
        {
            await using var context = Auth();
            return await new PasswordResetStore(context, new AccountPasswordService(), cache)
                .ReplacePasswordAsync(identity, "ResetPassword" + index + "!", default);
        });
        Assert.That(results.Count(result => result), Is.EqualTo(1));
        await using var check = Auth();
        Assert.That(await check.RefreshTokens!.AllAsync(token => token.RevokedAt != null), Is.True);
        Assert.That(await new PasswordResetStore(check, new AccountPasswordService(), cache)
            .ReplacePasswordAsync(identity, "CannotReuse123!", default), Is.False);
    }

    [Test]
    public async Task Routing_refresh_uses_real_triggers_across_independent_connections()
    {
        int keyId;
        await using (var context = Keys())
        {
            var key = new SupplierKey { Enable = true, BaseUrl = "https://example.invalid", ApiKey = "test-only" };
            key.ModelKeyBinds = [new ModelKeyBind { Enable = true, SupplierKey = key, Fee = 1,
                ProviderModelValue = "provider-test", Model = new Model { Name = "Test", ModelValue = "logical-test" } }];
            context.Add(key);
            await context.SaveChangesAsync();
            keyId = key.SupplierKeyId;
        }
        var models = new[] { new QuickModel(), new QuickModel() };
        var gates = new[] { new RouteReloadGate(TimeProvider.System), new RouteReloadGate(TimeProvider.System) };
        async Task Refresh(int index)
        {
            await using var context = Keys();
            await new KeyLoader(context, new StableKeyPoolRepository(new ModelKeyBuilder(), models[index]), gates[index]).LoadKeys();
        }
        await Refresh(0); await Refresh(1);
        Assert.That(models.All(model => model.IsModelAvailable("logical-test")), Is.True);
        var previous = gates[0].LoadedRevision;
        await using (var context = Keys())
            await context.SupplierKeys!.Where(key => key.SupplierKeyId == keyId)
                .ExecuteUpdateAsync(update => update.SetProperty(key => key.Enable, false));
        await Refresh(0); await Refresh(1);
        Assert.That(models.All(model => !model.IsModelAvailable("logical-test")), Is.True);
        Assert.That(gates[0].LoadedRevision, Is.GreaterThan(previous!.Value));
    }

    [Test]
    public async Task Policy_role_cleanup_task_receipts_and_usage_use_the_complete_production_schema()
    {
        int roleId;
        await using (var context = Auth())
        {
            var role = new Role { Code = "reviewer", Name = "Reviewer" };
            context.Roles!.Add(role);
            context.AccountRoles!.Add(new AccountRole { AccountId = adminId, Role = role });
            await context.SaveChangesAsync(); roleId = role.RoleId;
        }
        var store = new FeatureAccessStore(configuration);
        var policy = FeaturePolicies.Empty;
        policy.Features[0].Roles["reviewer"] = new(true, 3, 10, 2);
        Assert.That(await store.Save(policy, default), Is.True);
        var stale = await store.Read(default);
        var lease = await store.Acquire(adminId, FeaturePolicies.Resolve(stale, "chat", ["reviewer"]), default);
        Assert.That(lease.LeaseId, Is.Not.Null);
        await store.RecordTask(adminId, "CaseSensitiveTask", default, 123);
        Assert.That(await store.OwnsTask(adminId, "casesensitivetask", default), Is.False);
        Assert.That(await store.GetTaskSupplierKeyId(adminId, "CaseSensitiveTask", default), Is.EqualTo(123));
        var usage = await new FeatureUsageReader(configuration).ReadUsage(adminId, default);
        Assert.That(usage.Features.Single(feature => feature.Policy.Key == "chat").DayUsed, Is.EqualTo(1));
        Assert.That(usage.Features.Single(feature => feature.Policy.Key == "chat").ActiveRequests, Is.EqualTo(1));
        await using (var context = Auth())
        {
            var roles = new RoleRepository(context, Sessions(context));
            await roles.DeleteRoleByIdAsync(roleId);
            await roles.AddRoleAsync("Recreated", "reviewer");
        }
        Assert.That(await store.Save(stale, default), Is.False, "Reusing a role code must not restore a deleted grant.");
        Assert.That((await store.Read(default)).Features.All(feature => !feature.Roles.ContainsKey("reviewer")), Is.True);
        Assert.That(await store.OwnsTask(adminId, "CaseSensitiveTask", default), Is.True);
        await store.Release(lease.LeaseId!, default);
    }

    [Test]
    public async Task Routing_poll_reads_a_committed_snapshot_without_waiting_for_the_wal_writer()
    {
        int keyId;
        await using (var context = Keys())
        {
            var key = new SupplierKey { Enable = true, BaseUrl = "https://example.invalid", ApiKey = "test-only" };
            key.ModelKeyBinds = [new ModelKeyBind { Enable = true, SupplierKey = key, Fee = 1,
                ProviderModelValue = "provider-test", Model = new Model { Name = "Test", ModelValue = "logical-test" } }];
            context.Add(key); await context.SaveChangesAsync(); keyId = key.SupplierKeyId;
        }
        await using var writer = Keys();
        await using var transaction = await writer.Database.BeginTransactionAsync();
        await writer.SupplierKeys!.Where(key => key.SupplierKeyId == keyId)
            .ExecuteUpdateAsync(update => update.SetProperty(key => key.Enable, false));
        var models = new QuickModel();
        var gate = new RouteReloadGate(TimeProvider.System);
        async Task Refresh()
        {
            await using var reader = Keys();
            await new KeyLoader(reader, new StableKeyPoolRepository(new ModelKeyBuilder(), models), gate).LoadKeys();
        }
        var poll = Task.Run(Refresh);
        Task completed;
        try
        {
            completed = await Task.WhenAny(poll, Task.Delay(TimeSpan.FromSeconds(2)));
            if (completed == poll)
            {
                await poll;
                Assert.That(models.IsModelAvailable("logical-test"), Is.True, "Uncommitted disable must remain invisible.");
            }
        }
        finally { await transaction.CommitAsync(); }
        await poll;
        Assert.That(completed, Is.SameAs(poll), "A read-only poll must not wait to acquire the writer lock.");
        await Refresh();
        Assert.That(models.IsModelAvailable("logical-test"), Is.False);
    }

    [Test]
    public async Task Production_schema_preserves_datetime_json_identity_and_foreign_key_cascades()
    {
        var date = new DateTime(2026, 11, 6, 10, 20, 30, DateTimeKind.Utc).AddTicks(1234567);
        long historyId;
        await using (var context = Auth())
        {
            context.GenerateTasks!.AddRange(new GenerateTask { TaskId = "z", AccountId = adminId, DateTime = date,
                Images = ["one"], Videos = ["video"] }, new GenerateTask { TaskId = "a", AccountId = adminId, DateTime = date });
            var history = new ChatHistory { UserId = adminId, DataId = 1, Title = "test" };
            context.ChatHistories!.Add(history);
            context.ChatMessages!.Add(new ChatMessage { ChatHistory = history, DataId = 1, Content = "message",
                FileList = [new FileAdds { FileName = "test.txt", FileContent = "text" }] });
            await context.SaveChangesAsync();
            historyId = history.ChatHistoryId;
            Assert.That(historyId, Is.GreaterThan(0));
            var task = await context.GenerateTasks.SingleAsync(task => task.TaskId == "z");
            task.Images!.Add("two"); task.Videos![0] = "changed";
            await context.SaveChangesAsync();
        }
        await using (var context = Auth())
        {
            var tasks = await new TaskRepository(context).GetGenerateTasks(adminId, []);
            Assert.That(tasks.Select(task => task.TaskId), Is.EqualTo(new[] { "a", "z" }));
            Assert.That(tasks[1].DateTime, Is.EqualTo(date));
            Assert.That(tasks[1].Images, Is.EqualTo(new[] { "one", "two" }));
            Assert.That(tasks[1].Videos, Is.EqualTo(new[] { "changed" }));
            await context.ChatHistories!.Where(history => history.ChatHistoryId == historyId).ExecuteDeleteAsync();
            Assert.That(await context.ChatMessages!.CountAsync(), Is.Zero);
            Assert.That(await context.FileAdds!.CountAsync(), Is.Zero);
            await context.Accounts!.Where(account => account.AccountId == adminId).ExecuteDeleteAsync();
            Assert.That(await context.GenerateTasks!.CountAsync(), Is.Zero);
            Assert.That(await context.AccountRoles!.CountAsync(), Is.Zero);
        }
    }

    private RegistrationService Registration(AuthContext context, RegistrationSender sender) => new(context,
        new AccountPasswordService(), sender, OptionsFactory.Create(new RegistrationOptions()),
        OptionsFactory.Create(new PasswordResetOptions { SmtpHost = "smtp.invalid", FromAddress = "no-reply@example.invalid" }),
        OptionsFactory.Create(jwt), TimeProvider.System, NullLogger<RegistrationService>.Instance);

    private static RegisterRequest Request(string email, string username, string code) => new()
        { Email = email, Username = username, EmailCode = code, Password = "Password123!", Confirm = "Password123!" };

    private sealed class RegistrationSender : IRegistrationEmailSender
    {
        public ConcurrentDictionary<string, string> Codes { get; } = new();
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public Task SendAsync(string recipient, string code, CancellationToken token)
        {
            Codes[recipient] = code;
            Interlocked.Increment(ref calls);
            return Task.CompletedTask;
        }
    }
}
