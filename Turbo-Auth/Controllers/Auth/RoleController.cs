using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo_Auth.Models.Accounts;
using Turbo_Auth.Repositories.Accounts;

namespace Turbo_Auth.Controllers.Auth;
[ApiController]
[Route("api/[controller]")]
public class RoleController: Controller
{
    private IRoleRepository _roleRepository;
    public RoleController(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }
    
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAccountRoles([FromQuery]int? userId)
    {
        if (userId == null)
        {
            return Ok(await _roleRepository.GetRolesAsync());
        }
        var roles = await _roleRepository.GetRolesOfAccountAsync(userId.Value);
        return Ok(roles);
    }

    [HttpDelete("{roleId}")]
    [Authorize(Policy = "admin")]
    public async Task<IActionResult> DeleteRole(int roleId)
    {
        await _roleRepository.DeleteRoleByIdAsync(roleId);
        return Ok(new
        {
            msg = "删除成功"
        });
    }

    [HttpPut("{roleId}")]
    [Authorize(Policy = "admin")]
    public async Task<IActionResult> UpdateRole([FromBody]Role role,[FromRoute] int roleId)
    {
        if (role.RoleId != roleId)
        {
            return BadRequest("错误的参数，不匹配的实体。");
        }

        try
        {
            await _roleRepository.UpdateRoleAsync(role);
            return Ok(new
            {
                msg = "更新成功"
            });
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
        
    }

    [HttpPost]
    [Authorize(Policy = "admin")]
    public async Task<IActionResult> AddRole(Role role)
    {
        try
        {
            await _roleRepository.AddRoleAsync(role.Name!);
            return Ok(new
            {
                msg = "添加成功"
            });
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }

    
    
}