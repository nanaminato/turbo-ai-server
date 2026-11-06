using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Turbo.Auth.Data;
using Turbo.Auth.Bootstrap;
using Turbo.Auth.Data.Contexts;

namespace Turbo.Auth.Tests;

public class DatabaseProviderTests
{
    private static IConfiguration Configuration(string? provider, string? connection) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = provider, ["ConnectionStrings:ciko"] = connection
        }).Build();

    [TestCase(null, null, "Production")]
    [TestCase("", "", "Production")]
    [TestCase("Development", null, "Development")]
    [TestCase(null, "Development", "Development")]
    [TestCase("Staging", "Development", "Staging")]
    [TestCase("", "Staging", "Staging")]
    public void Setup_environment_matches_webapplication_precedence(string? dotnetEnvironment, string? aspnetEnvironment, string expected) =>
        Assert.That(DatabaseSetupCommand.ResolveEnvironment(dotnetEnvironment, aspnetEnvironment), Is.EqualTo(expected));

    [Test]
    public void Existing_configuration_defaults_to_mysql_without_changing_connection_or_opening_it()
    {
        const string connection = "Server=localhost;Database=compatibility;User ID=test;Password=not-real;";
        var configuration = Configuration(null, connection);
        var provider = DatabaseProvider.FromConfiguration(configuration);
        Assert.That(provider.IsSqlite, Is.False);
        Assert.That(provider.ConnectionString, Is.EqualTo(connection));
        using var raw = provider.CreateConnection();
        Assert.That(raw, Is.TypeOf<MySqlConnection>());
        using var auth = new AuthContext(configuration);
        using var catalog = new KeyContext(configuration);
        Assert.That(auth.Database.IsMySql(), Is.True);
        Assert.That(catalog.Database.IsMySql(), Is.True);
    }

    [TestCase("Postgres", "Data Source=/tmp/x.db")]
    [TestCase("", "Data Source=/tmp/x.db")]
    [TestCase("Sqlite", null)]
    [TestCase("Sqlite", "Data Source=:memory:")]
    [TestCase("Sqlite", "Data Source=relative.db")]
    [TestCase("Sqlite", "Data Source=file:/tmp/x.db")]
    [TestCase("Sqlite", "Data Source=/tmp/x.db;Mode=Memory")]
    [TestCase("Sqlite", "Data Source=/tmp/x.db;Mode=ReadOnly")]
    public void Invalid_configuration_is_rejected_instead_of_falling_back(string provider, string? connection) =>
        Assert.Throws<InvalidOperationException>(() => DatabaseProvider.FromConfiguration(Configuration(provider, connection)));

    [Test]
    public void Sqlite_uses_same_provider_for_both_contexts_and_never_creates_a_missing_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "relaxkon-missing-" + Guid.NewGuid().ToString("N") + ".db");
        var configuration = Configuration("sqlite", $"Data Source={path};Foreign Keys=False;Cache=Shared;Default Timeout=999;Pooling=False");
        var provider = DatabaseProvider.FromConfiguration(configuration);
        var settings = new SqliteConnectionStringBuilder(provider.ConnectionString);
        Assert.Multiple(() =>
        {
            Assert.That(settings.ForeignKeys, Is.True);
            Assert.That(settings.Mode, Is.EqualTo(SqliteOpenMode.ReadWrite));
            Assert.That(settings.Cache, Is.EqualTo(SqliteCacheMode.Private));
            Assert.That(settings.DefaultTimeout, Is.EqualTo(60));
            Assert.That(settings.Pooling, Is.False);
        });
        using var auth = new AuthContext(configuration);
        using var catalog = new KeyContext(configuration);
        Assert.That(auth.Database.IsSqlite(), Is.True);
        Assert.That(catalog.Database.IsSqlite(), Is.True);
        using var connection = provider.CreateConnection();
        Assert.Throws<SqliteException>(() => connection.Open());
        Assert.That(File.Exists(path), Is.False);
    }
}
