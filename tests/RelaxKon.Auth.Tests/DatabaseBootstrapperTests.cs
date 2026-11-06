using RelaxKon.Auth.Bootstrap;

namespace RelaxKon.Auth.Tests;

public class DatabaseBootstrapperTests
{
    [TestCase("mysql")]
    [TestCase("MYSQL")]
    [TestCase("information_schema")]
    [TestCase("sys")]
    [TestCase("performance_schema")]
    [TestCase("")]
    [TestCase("db`; DROP DATABASE mysql;--")]
    [TestCase("db-name")]
    public void Initialization_rejects_system_or_unsafe_database_names(string database)
    {
        Assert.Throws<ArgumentException>(() => DatabaseBootstrapper.ValidateDatabaseName(database));
    }

    [TestCase("turboai")]
    [TestCase("niko")]
    [TestCase("test_123")]
    public void Initialization_accepts_application_database_names(string database)
    {
        Assert.That(DatabaseBootstrapper.ValidateDatabaseName(database), Is.EqualTo(database));
    }

    [Test]
    public void Embedded_seed_has_roles_but_no_orphan_bindings_or_default_account()
    {
        var seed = DatabaseBootstrapper.ReadScript("Seed");
        Assert.That(seed, Does.Contain("insert into Roles"));
        Assert.That(seed, Does.Not.Contain("insert into AccountRoles").IgnoreCase);
        Assert.That(seed, Does.Not.Contain("insert into Accounts ").IgnoreCase);
        Assert.That(DatabaseBootstrapper.ReadScript("Schema"), Does.Contain("create table RefreshTokens"));
    }

    [TestCase("ab", "admin@example.com", "Password123!")]
    [TestCase("admin", "not-an-email", "Password123!")]
    [TestCase("admin", "admin@example.com", "short")]
    [TestCase("admin", "admin@example.com", "123456789012345678901")]
    public void Invalid_admin_input_is_rejected_before_connecting(string username, string email, string password)
    {
        Assert.Throws<ArgumentException>(() => new BootstrapAdmin(username, email, password).Validate());
    }

    [Test]
    public void Admin_input_accepts_login_compatible_password_and_redacts_diagnostics()
    {
        var admin = new BootstrapAdmin("admin", "admin@localhost", "randomPassword123!");
        Assert.DoesNotThrow(admin.Validate);
        Assert.That(admin.ToString(), Does.Not.Contain(admin.Password));
    }
}
