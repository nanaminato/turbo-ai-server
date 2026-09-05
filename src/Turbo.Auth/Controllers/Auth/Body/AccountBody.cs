using System.ComponentModel.DataAnnotations;
using Turbo.Auth.Models.Accounts;

namespace Turbo.Auth.Controllers.Auth.Body;

public class AccountBody
{
    public int AccountId { get; set; }
    [MaxLength(20)]
    public string? Username { get; set; }
    [MaxLength(128)]
    [DataType(DataType.Password)]
    public string? Password { get; set; }
    [MaxLength(50)]
    public string? Email { get; set; }
    
    public ICollection<Role>? UserRoles { get; set; }
}
