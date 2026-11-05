using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Turbo.Auth.Models.Accounts;
public class Role
{
    [Key] public int RoleId { get; set; }
    [Required, MaxLength(64)] public string? Code { get; set; }
    [Required, MaxLength(64)] public string? Name { get; set; }
    [MaxLength(256)] public string? Description { get; set; }
    [NotMapped] public bool IsSystem => IsSystemCode(Code ?? Name);
    [NotMapped] public int MemberCount { get; set; }
    public static bool IsSystemCode(string? code) => code is "admin" or "user" or "vip";
    public override string ToString() => $"Role {RoleId}: {Code}";
}
