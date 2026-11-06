using RelaxKon.Auth.Application.Sync;
using Microsoft.EntityFrameworkCore;
using RelaxKon.Auth.Models.Accounts;
using RelaxKon.Auth.Models.Sync.Messages;
using RelaxKon.Auth.Models.Sync.Tasks;


namespace RelaxKon.Auth.Data.Contexts;

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
        if (!optionsBuilder.IsConfigured) DatabaseProvider.FromConfiguration(_configuration).Configure(optionsBuilder);
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (Database.IsMySql())
            modelBuilder.UseCollation("utf8mb4_0900_ai_ci").HasCharSet("utf8mb4");

        modelBuilder.Entity<SyncAccount>(entity =>
        {
            entity.ToTable("SyncAccounts"); entity.HasKey(item => item.AccountId);
            entity.Property(item => item.AccountId).ValueGeneratedNever();
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<SyncChange>(entity =>
        {
            entity.ToTable("SyncChanges"); entity.HasKey(item => new { item.AccountId, item.Version });
            entity.HasOne<SyncAccount>().WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.AccountId, item.OperationId }).IsUnique();
            entity.Property(item => item.OperationId).HasMaxLength(32);
            entity.Property(item => item.RequestHash).HasMaxLength(64);
            entity.Property(item => item.Kind).HasMaxLength(16);
            entity.Property(item => item.Action).HasMaxLength(16);
            entity.Property(item => item.EntityId).HasMaxLength(255);
        });
        modelBuilder.Entity<ChatHistory>().HasIndex(item => new { item.UserId, item.DataId }).IsUnique();
        modelBuilder.Entity<ChatMessage>().HasIndex(item => new { item.ChatHistoryId, item.DataId }).IsUnique();
        modelBuilder.Entity<RelaxKon.Auth.Security.Registration.RegistrationEmailChallenge>(entity =>
        {
            entity.ToTable("RegistrationEmailChallenges"); entity.HasKey(item => item.EmailHash);
            entity.Property(item => item.EmailHash).HasMaxLength(64);
            entity.Property(item => item.CodeHash).HasMaxLength(64);
        });
        OnModelCreatingPartial(modelBuilder);
        modelBuilder.Entity<Account>().ToTable("Accounts");
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasIndex(token => new { token.AccountId, token.SessionId });
            entity.HasIndex(token => token.ExpiresAt);
            entity.HasOne(token => token.Account)
                .WithMany()
                .HasForeignKey(token => token.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AccountRole>().ToTable("AccountRoles");
        modelBuilder.Entity<Role>().ToTable("Roles");
        modelBuilder.Entity<Role>().HasIndex(role => role.Code).IsUnique();
        modelBuilder.Entity<AccountRole>().HasIndex(link => new { link.AccountId, link.RoleId }).IsUnique();
        modelBuilder.Entity<ChatHistory>().ToTable("ChatHistories");
        modelBuilder.Entity<ChatMessage>().ToTable("ChatMessages");
        modelBuilder.Entity<FileAdds>().ToTable("FileAdds");
        ConfigureRuntimeModel(modelBuilder);
        if (Database.IsSqlite())
        {
            // SQLite NOCASE is ASCII-only, not MySQL Unicode/accent collation.
            modelBuilder.Entity<Account>().Property(item => item.Username).UseCollation("NOCASE");
            modelBuilder.Entity<Account>().Property(item => item.Email).UseCollation("NOCASE");
            modelBuilder.Entity<Role>().Property(item => item.Name).UseCollation("NOCASE");
            modelBuilder.Entity<Role>().Property(item => item.Code).UseCollation("BINARY");
        }
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
    public DbSet<RefreshToken>? RefreshTokens
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
