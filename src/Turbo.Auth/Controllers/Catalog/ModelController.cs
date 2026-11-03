using Turbo.Auth.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Models.Suppliers;
using Turbo.Auth.Repositories.Catalog;

namespace Turbo.Auth.Controllers.Catalog;
[ApiController]
[Authorize(Policy = "admin")]
[Route("api/[controller]")]
public class ModelController: Controller
{
    private IModelRepository _modelRepository;

    public ModelController(IModelRepository modelRepository)
    {
        _modelRepository = modelRepository;
    }

    [HttpPost("changeModelStatus/{modelId}")]
    public async Task<IActionResult> ChangeModelStatus(int modelId,ChangeModelStatus changeModelStatus)
    {
        if (modelId != changeModelStatus.ModelId)
        {
            return BadRequest(ApiMessages.Get("invalid_parameters"));
        }
        try
        {
            await _modelRepository.SetEnableStatus(changeModelStatus.ModelId, changeModelStatus.Enable);
        }
        catch (Exception e)
        {
            return BadRequest(ApiMessages.Get("operation_failed"));
        }

        return Ok(
        new {
            msg = ApiMessages.Get("operation_succeeded")
        });
    }
    

    [HttpGet]
    public async Task<List<Model>?> GetModelsWithKey([FromQuery] int? keyId)
    {
        if (keyId == null)
        {
            return await _modelRepository.GetModelsAsync();
        }

        return await _modelRepository.GetModelsOfKeyAsync(keyId.Value);
    }

    [HttpDelete("{modelId}")]
    public async Task<IActionResult> DeleteModel(int modelId)
    {
        try
        {
            await _modelRepository.DeleteModelByIdAsync(modelId);
            return Ok();
        }
        catch (Exception e)
        {
            return BadRequest();
        }
    }

    [HttpPost]
    public async Task<IActionResult> AddModel(Model model)
    {
        try
        {
            await _modelRepository.AddModelAsync(model);
            return Ok();
        }
        catch (Exception e)
        {
            return BadRequest();
        }
    }

    [HttpPut("{modelId}")]
    public async Task<IActionResult> UpdateModel(Model model,int modelId)
    {
        if (model.ModelId != modelId)
        {
            return BadRequest(ApiMessages.Get("parameters_mismatch"));
        }

        try
        {
            await _modelRepository.UpdateModelAsync(model);
            return Ok();
        }
        catch (Exception e)
        {
            return BadRequest(ApiMessages.Get("execution_failed"));
        }
    }
}

public class ChangeModelStatus
{
    public int ModelId
    {
        get;
        set;
    }
    public bool Enable
    {
        get;
        set;
    }
}