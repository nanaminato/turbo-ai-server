using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo_Auth.Handlers.Loader;

namespace Turbo_Auth.Controllers.KeyLoad;

[ApiController]
[Authorize(Policy = "admin")]
[Route("api/sync")]
public class SyncController: Controller
{
    private IKeyLoader _keyLoader;
    public SyncController(IKeyLoader keyLoader)
    {
        _keyLoader = keyLoader;
    }

    [HttpPost("loadKeys")]
    public async Task<IActionResult> ReloadKeys()
    {
        try
        {
            await _keyLoader.LoadKeys();
            return Ok(new{msg="加载密钥成功"});
        }
        catch (Exception e)
        {
            return BadRequest($"加载密钥失败！{e.Message}");
        }
        
    }
}