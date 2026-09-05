using System.ComponentModel.DataAnnotations;

namespace Turbo.Auth.Models.Accounts;

/// <summary>
/// A server-side session record. The client only ever receives the opaque raw token;
/// this entity stores its HMAC hash.
/// </summary>
public class RefreshToken
{
    [Key]
    public Guid RefreshTokenId { get; set; }

    public int AccountId { get; set; }

    [Required]
    [MaxLength(64)]
    public string TokenHash { get; set; } = null!;

    public Guid SessionId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }

    [MaxLength(256)]
    public string? DeviceName { get; set; }

    [MaxLength(64)]
    public string? CreatedByIp { get; set; }

    public Account? Account { get; set; }
}
