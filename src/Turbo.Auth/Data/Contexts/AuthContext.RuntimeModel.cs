using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turbo.Auth.Models.Accounts;
using Turbo.Auth.Models.Sync.Tasks;

namespace Turbo.Auth.Data.Contexts;

public partial class AuthContext
{
    // Converted JSON collections need structural equality and a detached snapshot:
    // otherwise EF misses Images.Add/Remove or Videos[index] changes in tracked tasks.
    private static readonly ValueComparer<List<string>?> MediaListComparer = new(
        (left, right) => left == null ? right == null : right != null && left.SequenceEqual(right),
        list => list == null ? 0 : list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item == null ? 0 : StringComparer.Ordinal.GetHashCode(item))),
        list => list == null ? null : list.ToList());

    private static void ConfigureMedia(PropertyBuilder<List<string>?> property)
    {
        property.HasColumnType("json").HasConversion(
            list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null));
        property.Metadata.SetValueComparer(MediaListComparer);
    }

    private static void ConfigureRuntimeModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            // Login, reset and registration look up these fields independently.
            // Nonunique indexes preserve legacy duplicate data and current semantics.
            entity.HasIndex(account => account.Username).HasDatabaseName("IX_Accounts_Username");
            entity.HasIndex(account => account.Email).HasDatabaseName("IX_Accounts_Email");
        });
        modelBuilder.Entity<RefreshToken>().HasIndex(token => new { token.AccountId, token.RevokedAt, token.ExpiresAt })
            .HasDatabaseName("IX_RefreshTokens_AccountId_RevokedAt_ExpiresAt");
        modelBuilder.Entity<GenerateTask>(entity =>
        {
            entity.ToTable("GenerateTasks");
            entity.Property(task => task.TaskId).HasMaxLength(255);
            entity.HasIndex(task => new { task.AccountId, task.DateTime, task.TaskId })
                .HasDatabaseName("IX_GenerateTasks_AccountId_DateTime_TaskId");
            ConfigureMedia(entity.Property(task => task.Images));
            ConfigureMedia(entity.Property(task => task.Videos));
        });
    }
}
