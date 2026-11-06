using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Contracts.Common;
using Turbo.Auth.Contracts.FeatureAccess;

namespace Turbo.Auth.Security.FeatureAccess;

[ApiController]
[Authorize]
[Route("api/feature-access/usage")]
public sealed class FeatureUsageController(IFeatureUsageReader reader, ILogger<FeatureUsageController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<FeatureUsageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<CodeResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<FeatureUsageResponse>> Get(CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        var accountId = FeatureIdentity.AccountId(User);
        if (accountId is null) return Unauthorized();
        try
        {
            return Ok(FeatureUsageResponse.From(await reader.ReadUsage(accountId.Value, token)));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Feature usage snapshot unavailable");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new CodeResponse("feature_access_unavailable"));
        }
    }
}
