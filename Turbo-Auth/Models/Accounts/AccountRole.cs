using System.ComponentModel.DataAnnotations;

namespace Turbo_Auth.Models.Accounts;

public class AccountRole
{
    [Key]
    public int AccountRoleId { get; set; }
    public int AccountId { get; set;}
    public Account? Account{get;set;}

    public int RoleId{get;set;}
    public Role? Role{get;set;}
    public override string ToString()
    {
        return $"AccountRoleId: {AccountRoleId}, AccountId: {AccountId}, RoleId: {RoleId}";
    }
}
