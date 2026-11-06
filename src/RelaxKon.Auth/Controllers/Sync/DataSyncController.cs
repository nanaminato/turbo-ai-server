using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RelaxKon.Auth.Application.Sync;
using RelaxKon.Auth.Contracts.Sync;
using RelaxKon.Auth.Contracts.Common;
using RelaxKon.Auth.Security.FeatureAccess;

namespace RelaxKon.Auth.Controllers.Sync;

[ApiController, Authorize(Policy = "user"), Route("api/sync/data")]
public sealed class DataSyncController(DataSyncService sync) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SyncBatchResponse>> Read(long cursor = 0, CancellationToken token = default)
    {
        if (Request.Headers["X-Sync-Account"] != FeatureIdentity.AccountId(User)!.Value.ToString()) return Conflict(new CodeResponse("sync_account_changed"));
        try { return Ok(SyncBatchResponse.From(await sync.Read(FeatureIdentity.AccountId(User)!.Value, cursor, token))); }
        catch (ArgumentException) { return BadRequest(new CodeResponse("invalid_sync_cursor")); }
        catch (SyncConflictException) { return Conflict(new CodeResponse("sync_conflict")); }
    }

    [HttpPost]
    public async Task<ActionResult<SyncVersionResponse>> Apply(SyncMutationRequest operation, CancellationToken token)
    {
        if (Request.Headers["X-Sync-Account"] != FeatureIdentity.AccountId(User)!.Value.ToString()) return Conflict(new CodeResponse("sync_account_changed"));
        try { return Ok(new SyncVersionResponse(await sync.Apply(FeatureIdentity.AccountId(User)!.Value, operation.ToApplication(), token))); }
        catch (ArgumentException) { return BadRequest(new CodeResponse("invalid_sync_operation")); }
        catch (SyncConflictException) { return Conflict(new CodeResponse("sync_conflict")); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
