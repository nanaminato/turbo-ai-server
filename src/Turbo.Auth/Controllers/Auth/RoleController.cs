using Turbo.Auth.Contracts.Auth;
using Turbo.Auth.Contracts.Common;
using Turbo.Auth.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Models.Accounts;
using Turbo.Auth.Repositories.Accounts;

namespace Turbo.Auth.Controllers.Auth;

[ApiController]
[Route("api/[controller]")]
public class RoleController : Controller
{
    private IRoleRepository _roleRepository;
    public RoleController(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> GetAccountRoles([FromQuery] int? userId)
    {
        if (userId == null)
        {
            return Ok((await _roleRepository.GetRolesAsync() ?? []).Select(role => new RoleResponse(role.RoleId, role.Name)).ToList());
        }
        var roles = await _roleRepository.GetRolesOfAccountAsync(userId.Value);
        return Ok((roles ?? []).Select(role => new RoleResponse(role.RoleId, role.Name)).ToList());
    }

    [HttpDelete("{roleId}")]
    [Authorize(Policy = "admin")]
    public async Task<IActionResult> DeleteRole(int roleId)
    {
        await _roleRepository.DeleteRoleByIdAsync(roleId);
        return Ok(new MessageResponse(ApiMessages.Get("deleted")));
    }

    [HttpPut("{roleId}")]
    [Authorize(Policy = "admin")]
    public async Task<IActionResult> UpdateRole([FromBody] RoleWriteRequest role, [FromRoute] int roleId)
    {
        if (role.RoleId != 0 && role.RoleId != roleId)
        {
            return BadRequest(ApiMessages.Get("invalid_entity"));
        }

        try
        {
            await _roleRepository.UpdateRoleAsync(new Role { RoleId = roleId, Name = role.Name });
            return Ok(new MessageResponse(ApiMessages.Get("updated")));
        }
        catch (Exception)
        {
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = ApiMessages.Get("role_update_failed") });
        }

    }

    [HttpPost]
    [Authorize(Policy = "admin")]
    public async Task<IActionResult> AddRole(RoleWriteRequest role)
    {
        try
        {
            await _roleRepository.AddRoleAsync(role.Name!);
            return Ok(new MessageResponse(ApiMessages.Get("added")));
        }
        catch (Exception)
        {
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = ApiMessages.Get("role_create_failed") });
        }
    }



}
