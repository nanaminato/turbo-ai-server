using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turbo.Auth.Data.Contexts;

namespace Turbo.Auth.Security.FeatureAccess;

[ApiController]
[Authorize]
[Route("api/feature-access")]
public sealed class FeatureAccessController(FeatureAccessStore store, FeatureIdentity identity, AuthContext database) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        var id = FeatureIdentity.AccountId(User);
        if (id is null) return Unauthorized();
        var roles = await identity.Roles(id.Value, token);
        var document = await store.Read(token);
        return Ok(new { canManagePolicies = roles.Contains("admin", StringComparer.Ordinal),
            features = FeaturePolicies.Keys.Select(key => FeaturePolicies.Resolve(document, key, roles)) });
    }
    [HttpGet("policies")]
    public async Task<IActionResult> Get(CancellationToken token)
    {
        if (!await IsAdmin(token)) return Forbid();
        Response.Headers.CacheControl = "no-store";
        return Ok(await store.Read(token));
    }
    [HttpPut("policies")]
    [RequestSizeLimit(65536)]
    public async Task<IActionResult> Put(FeaturePolicyDocument document, CancellationToken token)
    {
        if (!await IsAdmin(token)) return Forbid();
        var names = await database.Roles!.AsNoTracking().Where(role => role.Name != null)
            .Select(role => role.Name!).ToListAsync(token);
        if (document.Revision < 0 || !FeaturePolicies.IsValid(document, names.ToHashSet(StringComparer.Ordinal)))
            return BadRequest(new { code = "invalid_feature_policy" });
        if (!await store.Save(document, token)) return Conflict(new { code = "feature_policy_conflict" });
        return Ok(document with { Revision = document.Revision + 1 });
    }
    private async Task<bool> IsAdmin(CancellationToken token) => FeatureIdentity.AccountId(User) is { } id &&
        (await identity.Roles(id, token)).Contains("admin", StringComparer.Ordinal);
}
