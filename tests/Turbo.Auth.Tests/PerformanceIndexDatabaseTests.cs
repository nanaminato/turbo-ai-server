using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Turbo.Auth.Tests;

public class PerformanceIndexDatabaseTests
{
    private const string RequiresMySql = "Requires TURBO_TEST_MYSQL; creates and removes isolated databases.";
    private const string IndexFingerprint = """
        SELECT TABLE_NAME,INDEX_NAME,NON_UNIQUE,GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)
        FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE()
        AND INDEX_NAME IN ('IX_Accounts_Username','IX_Accounts_Email',
        'IX_GenerateTasks_AccountId_DateTime_TaskId','IX_RefreshTokens_AccountId_RevokedAt_ExpiresAt')
        GROUP BY TABLE_NAME,INDEX_NAME,NON_UNIQUE ORDER BY TABLE_NAME,INDEX_NAME
        """;

    [Test, Explicit(RequiresMySql)]
    public async Task Additive_upgrade_is_idempotent_matches_fresh_indexes_and_preserves_every_affected_row()
    {
        await using var legacy = await MySqlTestDatabase.Create(legacy: true, upgrade: false);
        await legacy.Execute("""
            INSERT INTO RefreshTokens(RefreshTokenId,AccountId,TokenHash,SessionId,CreatedAt,ExpiresAt)
            VALUES ('11111111-1111-1111-1111-111111111111',1,REPEAT('a',64),
              '22222222-2222-2222-2222-222222222222','2026-01-01','2027-01-01')
            """);
        string[] snapshots = ["SELECT * FROM Accounts ORDER BY AccountId",
            "SELECT * FROM GenerateTasks ORDER BY TaskId", "SELECT * FROM RefreshTokens ORDER BY RefreshTokenId"];
        var before = new List<string[]>();
        foreach (var sql in snapshots) before.Add(await legacy.Rows(sql));
        Assert.That(await legacy.Rows(IndexFingerprint), Is.Empty);
        await legacy.Execute(MySqlTestDatabase.Resource("PerformanceIndexes.sql"));
        var first = await legacy.Rows(IndexFingerprint);
        await legacy.Execute(MySqlTestDatabase.Resource("PerformanceIndexes.sql"));
        Assert.That(await legacy.Rows(IndexFingerprint), Is.EqualTo(first), "Repeat execution must preserve definitions.");
        await using var fresh = await MySqlTestDatabase.Create();
        Assert.That(first, Is.EqualTo(await fresh.Rows(IndexFingerprint)));
        Assert.That(first, Has.Length.EqualTo(4));
        for (var i = 0; i < snapshots.Length; i++)
            Assert.That(await legacy.Rows(snapshots[i]), Is.EqualTo(before[i]), snapshots[i]);
    }

    [Test, Explicit(RequiresMySql)]
    public async Task Upgrade_refuses_a_same_named_index_with_a_different_definition_before_adding_any_index()
    {
        await using var database = await MySqlTestDatabase.Create(legacy: true, upgrade: false);
        await database.Execute("CREATE INDEX IX_Accounts_Email ON Accounts(Username)");
        Assert.ThrowsAsync<MySqlConnector.MySqlException>(() => database.Execute(MySqlTestDatabase.Resource("PerformanceIndexes.sql")));
        Assert.That(await database.Rows(IndexFingerprint), Is.EqualTo(new[] {"Accounts|IX_Accounts_Email|1|Username"}));
        Assert.That(await database.Scalar<long>("SELECT COUNT(*) FROM Accounts"), Is.EqualTo(2));
    }

    [TestCase(false), TestCase(true), Explicit(RequiresMySql)]
    public async Task Representative_lookup_task_ordering_and_active_session_queries_use_the_new_indexes(bool upgraded)
    {
        await using var database = await MySqlTestDatabase.Create(legacy: upgraded);
        await database.Execute("INSERT INTO Accounts(AccountId,Username,Password,Email) VALUES " +
            string.Join(",", Enumerable.Range(3, 998).Select(i => $"({i},'perf{i}','hash','perf{i}@example.invalid')")));
        var taskValues = Enumerable.Range(0, 6000).Select(i =>
        {
            var account = i < 2000 ? 1 : 2 + i % 999;
            return $"('perf-task-{i:D6}','image',{account},DATE_ADD('2026-01-01',INTERVAL {i} SECOND),JSON_ARRAY(),JSON_ARRAY())";
        });
        foreach (var chunk in taskValues.Chunk(500))
            await database.Execute("INSERT INTO GenerateTasks(TaskId,TaskType,AccountId,DateTime,Images,Videos) VALUES " + string.Join(",", chunk));
        var tokenValues = Enumerable.Range(0, 6000).Select(i =>
        {
            var account = i < 2000 ? 1 : 2 + i % 999;
            var revoked = i < 1990 ? "'2026-02-01'" : "NULL";
            return $"('{Guid.NewGuid()}',{account},'{i.ToString("x64", CultureInfo.InvariantCulture)}','{Guid.NewGuid()}','2026-01-01','2027-01-01',{revoked})";
        });
        foreach (var chunk in tokenValues.Chunk(500))
            await database.Execute("INSERT INTO RefreshTokens(RefreshTokenId,AccountId,TokenHash,SessionId,CreatedAt,ExpiresAt,RevokedAt) VALUES " + string.Join(",", chunk));
        await database.Execute("ANALYZE TABLE Accounts,GenerateTasks,RefreshTokens");
        await Uses("SELECT AccountId,Password FROM Accounts WHERE Username='perf777' LIMIT 1", "IX_Accounts_Username");
        await Uses("SELECT AccountId FROM Accounts WHERE Email='perf777@example.invalid' LIMIT 1", "IX_Accounts_Email");
        var taskPlan = await Uses("SELECT TaskId,TaskType,DateTime FROM GenerateTasks WHERE AccountId=1 ORDER BY DateTime DESC,TaskId DESC LIMIT 20",
            "IX_GenerateTasks_AccountId_DateTime_TaskId");
        Assert.That(taskPlan.SelectTokens("$..using_filesort").Any(value => value.Value<bool>()), Is.False);
        await Uses("SELECT RefreshTokenId,SessionId FROM RefreshTokens WHERE AccountId=1 AND RevokedAt IS NULL AND ExpiresAt>'2026-11-05'",
            "IX_RefreshTokens_AccountId_RevokedAt_ExpiresAt");

        async Task<JObject> Uses(string sql, string expected)
        {
            var plan = JObject.Parse(await database.Scalar<string>("EXPLAIN FORMAT=JSON " + sql));
            Assert.That(plan.SelectTokens("$..key").Select(value => value.Value<string>()), Does.Contain(expected), plan.ToString());
            TestContext.Progress.WriteLine($"EXPLAIN selected {expected} ({(upgraded ? "upgraded" : "fresh")}).");
            return plan;
        }
    }
}
