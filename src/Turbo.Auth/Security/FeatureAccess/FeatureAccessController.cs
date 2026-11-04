using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turbo.Auth.Data.Contexts;
using Turbo.Auth.Contracts.Common;
using Turbo.Auth.Contracts.FeatureAccess;

namespace Turbo.Auth.Security.FeatureAccess;

[ApiController]
[Authorize]
[Route("api/feature-access")]
public sealed class FeatureAccessController(FeatureAccessStore store, FeatureIdentity identity, AuthContext database) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<FeatureAccessResponse>> Me(CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        var id = FeatureIdentity.AccountId(User);
        if (id is null) return Unauthorized();
        var roles = await identity.Roles(id.Value, token);
        var document = await store.Read(token);
        return Ok(new FeatureAccessResponse(roles.Contains("admin", StringComparer.Ordinal),
            FeaturePolicies.Keys.Select(key => EffectiveFeatureResponse.From(FeaturePolicies.Resolve(document, key, roles))).ToList()));
    }
    [HttpGet("policies")]
    public async Task<ActionResult<FeaturePolicyContractDocument>> Get(CancellationToken token)
    {
        if (!await IsAdmin(token)) return Forbid();
        Response.Headers.CacheControl = "no-store";
        return Ok(FeaturePolicyContractDocument.From(await store.Read(token)));
    }
    [HttpPut("policies")]
    [RequestSizeLimit(65536)]
    public async Task<ActionResult<FeaturePolicyContractDocument>> Put(FeaturePolicyContractDocument request, CancellationToken token)
    {
        if (!await IsAdmin(token)) return Forbid();
        var document = request.ToApplication();
        var names = await database.Roles!.AsNoTracking().Where(role => role.Name != null)
            .Select(role => role.Name!).ToListAsync(token);
        if (document.Revision < 0 || !FeaturePolicies.IsValid(document, names.ToHashSet(StringComparer.Ordinal)))
            return BadRequest(new CodeResponse("invalid_feature_policy"));
        if (!await store.Save(document, token)) return Conflict(new CodeResponse("feature_policy_conflict"));
        return Ok(FeaturePolicyContractDocument.From(document with { Revision = document.Revision + 1 }));
    }
    private async Task<bool> IsAdmin(CancellationToken token) => FeatureIdentity.AccountId(User) is { } id &&
        (await identity.Roles(id, token)).Contains("admin", StringComparer.Ordinal);
}
