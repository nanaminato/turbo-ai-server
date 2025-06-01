using System.ComponentModel.DataAnnotations;
using Turbo_Auth.Models.Accounts;

namespace Turbo_Auth.Controllers.Auth.Body;

public class AccountBody
{
    public int AccountId { get; set; }
    [MaxLength(20)]
    public string? Username { get; set; }
    [MaxLength(20)]
    public string? Password { get; set; }
    [MaxLength(50)]
    public string? Email { get; set; }
    
    public ICollection<Role>? UserRoles { get; set; }
}