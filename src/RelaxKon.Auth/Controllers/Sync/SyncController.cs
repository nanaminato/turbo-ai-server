using RelaxKon.Auth.Localization;
using RelaxKon.Auth.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RelaxKon.Auth.Application.Routing;

namespace RelaxKon.Auth.Controllers.Sync;

[ApiController]
[Authorize(Policy = "admin")]
[Route("api/sync")]
public class SyncController : Controller
{
    private IKeyLoader _keyLoader;
    public SyncController(IKeyLoader keyLoader)
    {
        _keyLoader = keyLoader;
    }

    [HttpPost("loadKeys")]
    public async Task<ActionResult<MessageResponse>> ReloadKeys()
    {
        try
        {
            await _keyLoader.LoadKeys(HttpContext.RequestAborted);
            return Ok(new MessageResponse(ApiMessages.Get("keys_loaded")));
        }
        catch (Exception)
        {
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = ApiMessages.Get("keys_load_failed") });
        }

    }
}
