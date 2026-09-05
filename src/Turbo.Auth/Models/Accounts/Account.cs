using System.ComponentModel.DataAnnotations;

namespace Turbo.Auth.Models.Accounts;

public class Account
{
    [Key]
    public int AccountId { get; set; }
    [Required]
    [MaxLength(20)]
    public string? Username { get; set; }
    [Required]
    [MaxLength(512)]
    public string? Password { get; set; }
    [Required]
    [MaxLength(64)]
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    [MaxLength(50)]
    [Required]
    public string? Email { get; set; }
    
    public ICollection<AccountRole>? UserRoles { get; set; }
    override 
    public string ToString()
    {
        return $"AccountId: {AccountId}, Username: {Username}, Email: {Email}";
    }
}
