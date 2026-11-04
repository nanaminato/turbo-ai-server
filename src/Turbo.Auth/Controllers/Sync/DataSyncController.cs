using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Application.Sync;
using Turbo.Auth.Security.FeatureAccess;

namespace Turbo.Auth.Controllers.Sync;

[ApiController, Authorize(Policy = "user"), Route("api/sync/data")]
public sealed class DataSyncController(DataSyncService sync) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(long cursor = 0, CancellationToken token = default)
    {
        if (Request.Headers["X-Sync-Account"] != FeatureIdentity.AccountId(User)!.Value.ToString()) return Conflict(new {code = "sync_account_changed"});
        try { return Ok(await sync.Read(FeatureIdentity.AccountId(User)!.Value, cursor, token)); }
        catch (ArgumentException) { return BadRequest(new { code = "invalid_sync_cursor" }); }
        catch (SyncConflictException) { return Conflict(new { code = "sync_conflict" }); }
    }

    [HttpPost]
    public async Task<IActionResult> Apply(SyncMutation operation, CancellationToken token)
    {
        if (Request.Headers["X-Sync-Account"] != FeatureIdentity.AccountId(User)!.Value.ToString()) return Conflict(new {code = "sync_account_changed"});
        try { return Ok(new { version = await sync.Apply(FeatureIdentity.AccountId(User)!.Value, operation, token) }); }
        catch (ArgumentException) { return BadRequest(new { code = "invalid_sync_operation" }); }
        catch (SyncConflictException) { return Conflict(new { code = "sync_conflict" }); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
