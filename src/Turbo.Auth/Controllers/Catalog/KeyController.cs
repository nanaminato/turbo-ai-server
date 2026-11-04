using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Contracts.Catalog;
using Turbo.Auth.Contracts.Common;
using Turbo.Auth.Repositories.Catalog;

namespace Turbo.Auth.Controllers.Catalog;

[ApiController, Authorize(Policy = "admin"), Route("api/[controller]")]
public sealed class KeyController(IKeyRepository keys) : ControllerBase
{
    [HttpGet("{keyId:int:min(1)}")]
    public async Task<ActionResult<KeyResponse>> GetKeyById(int keyId)
    {
        var key = await keys.GetKeyByIdAsync(keyId);
        return key is null ? NotFound() : Ok(KeyResponse.From(key));
    }
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KeyResponse>>> GetKeysWithModel([FromQuery] int? modelId)
    {
        var result = modelId.HasValue ? await keys.GetKeysWithModelAsync(modelId.Value) : await keys.GetKeysAsync();
        return Ok((result ?? []).Select(KeyResponse.From).ToList());
    }
    [HttpDelete("{keyId:int:min(1)}")]
    public async Task<IActionResult> DeleteKey(int keyId) { await keys.DeleteKeyByIdAsync(keyId); return Ok(); }
    [HttpPost]
    public async Task<IActionResult> AddKey(KeyWriteRequest request)
    {
        if (request.SupplierKeyId != 0) return BadRequest(ApiProblems.Create(400, "invalid_entity"));
        await keys.AddKeyAsync(request.ToEntity());
        return Ok();
    }
    [HttpGet("types")]
    public ActionResult<IReadOnlyList<ProviderTypeResponse>> GetKeyTypes() => Ok(ProviderCatalog.GetAll()
        .Select(provider => new ProviderTypeResponse(provider.DisplayName, (int)provider.Type)).ToList());
    [HttpPut("{keyId:int:min(1)}")]
    public async Task<IActionResult> UpdateKey(int keyId, KeyWriteRequest request)
    {
        if (request.SupplierKeyId != 0 && request.SupplierKeyId != keyId) return BadRequest(ApiProblems.Create(400, "parameters_mismatch"));
        await keys.UpdateKeyAsync(request.ToEntity(keyId));
        return Ok();
    }
}
