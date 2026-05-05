using Microsoft.EntityFrameworkCore;
using Turbo_Auth.Models.Accounts;
using Turbo_Auth.Models.ClientSyncs.Messages;
using Turbo_Auth.Models.Tasks;


namespace Turbo_Auth.Context;

public partial class AuthContext : DbContext
{
    public AuthContext(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private IConfiguration _configuration;
    public AuthContext(DbContextOptions<AuthContext> options, IConfiguration configuration)
        : base(options)
    {
        _configuration = configuration;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Console.WriteLine("CONNECT "+_configuration.GetConnectionString("ciko"));
        optionsBuilder.UseMySql(_configuration.GetConnectionString("ciko"), ServerVersion.Parse("8.4.6-mysql"));
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        OnModelCreatingPartial(modelBuilder);
        modelBuilder.Entity<Account>().ToTable("Accounts");
        modelBuilder.Entity<AccountRole>().ToTable("AccountRoles");
        modelBuilder.Entity<Role>().ToTable("Roles");
        modelBuilder.Entity<ChatHistory>().ToTable("ChatHistories");
        modelBuilder.Entity<ChatMessage>().ToTable("ChatMessages");
        modelBuilder.Entity<FileAdds>().ToTable("FileAdds");
        // modelBuilder.Entity<GenerateTask>().ToTable("GenerateTasks");
        modelBuilder.Entity<GenerateTask>(entity =>
        {
            entity.ToTable("GenerateTasks");
        
            // 配置 Images 字段为 JSON
            entity.Property(e => e.Images)
                .HasColumnType("json") // 指定数据库类型为 json
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions)null)
                );
        
            // 配置 Videos 字段为 JSON
            entity.Property(e => e.Videos)
                .HasColumnType("json") // 指定数据库类型为 json
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions)null)
                );
        });
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    public DbSet<Account>? Accounts
    {
        get;
        set;
    }

    public DbSet<AccountRole>? AccountRoles
    {
        get;
        set;
    }
    public DbSet<Role>? Roles
    {
        get;
        set;
    }
    public DbSet<ChatHistory>? ChatHistories
    {
        get;
        set;
    }

    public DbSet<ChatMessage>? ChatMessages
    {
        get;
        set;
    }

    public DbSet<FileAdds>? FileAdds
    {
        get;
        set;
    }

    public DbSet<GenerateTask>? GenerateTasks
    {
        get;
        set;
    }
}
