using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Contracts.Catalog;
using Turbo.Auth.Contracts.Common;
using Turbo.Auth.Localization;
using Turbo.Auth.Repositories.Catalog;

namespace Turbo.Auth.Controllers.Catalog;

[ApiController, Authorize(Policy = "admin"), Route("api/[controller]")]
public sealed class ModelController(IModelRepository models) : ControllerBase
{
    [HttpPost("changeModelStatus/{modelId:int:min(1)}")]
    public async Task<ActionResult<MessageResponse>> ChangeModelStatus(int modelId, ModelStatusRequest request)
    {
        if (modelId != request.ModelId) return BadRequest(ApiProblems.Create(400, "invalid_parameters"));
        await models.SetEnableStatus(modelId, request.Enable);
        return Ok(new MessageResponse(ApiMessages.Get("operation_succeeded")));
    }
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ModelResponse>>> GetModelsWithKey([FromQuery] int? keyId)
    {
        var result = keyId.HasValue ? await models.GetModelsOfKeyAsync(keyId.Value) : await models.GetModelsAsync();
        return Ok((result ?? []).Select(ModelResponse.From).ToList());
    }
    [HttpDelete("{modelId:int:min(1)}")]
    public async Task<IActionResult> DeleteModel(int modelId) { await models.DeleteModelByIdAsync(modelId); return Ok(); }
    [HttpPost]
    public async Task<IActionResult> AddModel(ModelWriteRequest request)
    {
        if (request.ModelId != 0) return BadRequest(ApiProblems.Create(400, "invalid_entity"));
        await models.AddModelAsync(request.ToEntity()); return Ok();
    }
    [HttpPut("{modelId:int:min(1)}")]
    public async Task<IActionResult> UpdateModel(ModelWriteRequest request, int modelId)
    {
        if (request.ModelId != 0 && request.ModelId != modelId) return BadRequest(ApiProblems.Create(400, "parameters_mismatch"));
        var model = request.ToEntity(); model.ModelId = modelId;
        await models.UpdateModelAsync(model); return Ok();
    }
}
