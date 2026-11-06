using System.Security.Cryptography;
using System.Text;
using MySqlConnector;
using Microsoft.Data.Sqlite;
using Turbo.Auth.Data;

namespace Turbo.Auth.Bootstrap;

public static class DatabaseSetupCommand
{
    public static string ResolveEnvironment(string? dotnetEnvironment, string? aspnetEnvironment) =>
        !string.IsNullOrWhiteSpace(dotnetEnvironment) ? dotnetEnvironment
        : !string.IsNullOrWhiteSpace(aspnetEnvironment) ? aspnetEnvironment : "Production";

    public static async Task<bool> TryRunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is not ("--init-db" or "--create-admin")) return false;
        if (args.Length != 1)
        {
            Console.Error.WriteLine("仅接受 --init-db 或 --create-admin；密码请通过隐藏输入提供，不要使用命令行参数。");
            Environment.ExitCode = 1;
            return true;
        }
        try
        {
            var environment = ResolveEnvironment(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"),
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables().Build();
            var providerName = configuration["Database:Provider"]?.Trim() ?? "MySql";
            if (!string.Equals(providerName, "MySql", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(providerName, "Sqlite", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Database:Provider must be MySql or Sqlite.");
            var sqlite = string.Equals(providerName, "Sqlite", StringComparison.OrdinalIgnoreCase);
            var configured = sqlite ? configuration.GetConnectionString("ciko") : Environment.GetEnvironmentVariable("ConnectionStrings__ciko");
            if (sqlite && string.IsNullOrWhiteSpace(configured))
                configured = new SqliteConnectionStringBuilder { DataSource = Prompt("SQLite 数据库绝对路径", Path.Combine(Directory.GetCurrentDirectory(), "relaxkon.db")) }.ConnectionString;
            if (string.IsNullOrWhiteSpace(configured))
            {
                var settings = new MySqlConnectionStringBuilder
                {
                    Server = Prompt("MySQL 主机", "localhost"),
                    Port = uint.Parse(Prompt("端口", "3306")),
                    Database = Prompt("数据库名", "turboai"),
                    UserID = Prompt("数据库用户", "root"),
                    Password = Secret("数据库密码: ")
                };
                configured = settings.ConnectionString;
            }
            configuration["ConnectionStrings:ciko"] = configured;
            var provider = DatabaseProvider.FromConfiguration(configuration);
            if (provider.IsSqlite)
                Console.WriteLine($"目标: SQLite {new SqliteConnectionStringBuilder(configured).DataSource}");
            else
            {
                var target = new MySqlConnectionStringBuilder(configured);
                DatabaseBootstrapper.ValidateDatabaseName(target.Database);
                Console.WriteLine($"目标: {target.Server}:{target.Port}/{target.Database}");
            }
            var username = Prompt("管理员用户名", "admin");
            var email = Prompt("管理员邮箱", "admin@localhost");
            var password = Secret("管理员密码（回车自动生成）: ");
            var generated = password.Length == 0;
            if (generated) password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(15));
            else if (password != Secret("再次输入管理员密码: "))
                throw new ArgumentException("两次输入的密码不一致。");
            var id = await new DatabaseBootstrapper().RunAsync(provider,
                new BootstrapAdmin(username, email, password), args[0] == "--init-db");
            Console.WriteLine($"完成：管理员 {username}，账户 ID {id}，角色 admin/user/vip。未启动 Web 服务。");
            if (generated) Console.WriteLine($"请保存管理员初始密码（仅显示一次）: {password}");
        }
        catch (SqliteException error)
        {
            Console.Error.WriteLine($"SQLite 操作失败（错误码 {error.SqliteErrorCode}），请检查文件路径、权限及数据库状态。已有数据未自动删除。");
            Environment.ExitCode = 1;
        }
        catch (MySqlException error)
        {
            Console.Error.WriteLine($"MySQL 操作失败（错误码 {error.Number}），请检查连接、权限及数据库状态。已有数据未自动删除。");
            Environment.ExitCode = 1;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException or EndOfStreamException)
        {
            Console.Error.WriteLine(error.Message);
            Environment.ExitCode = 1;
        }
        return true;
    }

    private static string Prompt(string label, string fallback)
    {
        Console.Write($"{label} [{fallback}]: ");
        var input = Console.ReadLine() ?? throw new EndOfStreamException("输入已结束，操作取消。");
        return string.IsNullOrWhiteSpace(input) ? fallback : input.Trim();
    }

    private static string Secret(string label)
    {
        Console.Write(label);
        // Redirected stdin supports deployment runners without putting passwords in argv or files.
        if (Console.IsInputRedirected)
            return Console.ReadLine() ?? throw new EndOfStreamException("输入已结束，操作取消。");
        var value = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
            if (key.Key == ConsoleKey.Backspace) { if (value.Length > 0) value.Length--; }
            else if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
        }
    }
}
