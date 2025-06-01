using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Turbo_Auth.Models.Accounts;

public class Role
{
    [Key]
    [JsonProperty("roleId")]
    public int RoleId { get; set; }
    [Required]
    [MaxLength(15)]
    [JsonProperty("name")]
    public string? Name { get; set; }

    public override string ToString()
    {
        return $"Role {this} . RoleId: {RoleId}, Name: {Name}";
    }
}