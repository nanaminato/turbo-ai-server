using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Contracts.Auth;
using Turbo.Auth.Contracts.Common;
using Turbo.Auth.Models.Accounts;
using Turbo.Auth.Repositories.Accounts;
using Turbo.Auth.Security;
namespace Turbo.Auth.Controllers.Auth;
[ApiController, Authorize, Route("api/role")]
public class RoleController(IRoleRepository roles, ILogger<RoleController>? logger = null) : ControllerBase
{
    private static RoleResponse ToResponse(Role role) => new(role.RoleId, role.Name, role.Code ?? role.Name, role.Description, role.IsSystem, role.MemberCount);
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> GetAccountRoles([FromQuery] int? userId)
    {
        if (!User.IsInRole("admin") && (userId is null || User.FindFirstValue(ClaimTypes.NameIdentifier) != userId.Value.ToString())) return Forbid();
        return Ok((userId is null ? await roles.GetRolesAsync() : await roles.GetRolesOfAccountAsync(userId.Value)).Select(ToResponse).ToList());
    }
    private ObjectResult Failure(RoleRuleException error) => StatusCode(error.Code.EndsWith("not_found") ? 404 : error.Code == "role_already_exists" ? 409 : 400, new CodeResponse(error.Code));
    private void Audit(string action, int? roleId) => logger?.LogInformation("Role management {Action}; actor {ActorId}; target {RoleId}", action, User.FindFirstValue(ClaimTypes.NameIdentifier), roleId);
    [HttpDelete("{roleId}"), Authorize(Policy = "admin")]
    public async Task<IActionResult> DeleteRole(int roleId)
    {
        try { await roles.DeleteRoleByIdAsync(roleId); Audit("delete", roleId); return Ok(new MessageResponse("deleted")); }
        catch (RoleRuleException error) { return Failure(error); }
    }
    [HttpPut("{roleId}"), Authorize(Policy = "admin")]
    public async Task<IActionResult> UpdateRole([FromBody] RoleWriteRequest role, [FromRoute] int roleId)
    {
        if (role.RoleId != 0 && role.RoleId != roleId) return BadRequest(new CodeResponse("invalid_entity"));
        try { await roles.UpdateRoleAsync(new Role { RoleId = roleId, Name = role.Name, Code = role.Code, Description = role.Description }); Audit("update", roleId); return Ok(new MessageResponse("updated")); }
        catch (RoleRuleException error) { return Failure(error); }
    }
    [HttpPost, Authorize(Policy = "admin")]
    public async Task<IActionResult> AddRole(RoleWriteRequest role)
    {
        if (role.RoleId != 0) return BadRequest(new CodeResponse("invalid_entity"));
        try { await roles.AddRoleAsync(role.Name, role.Code, role.Description); Audit("create", null); return Ok(new MessageResponse("added")); }
        catch (RoleRuleException error) { return Failure(error); }
    }
}
