using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RelaxKon.Auth.Contracts.Catalog;
using RelaxKon.Auth.Data.Contexts;

namespace RelaxKon.Auth.Controllers.OpenAPI;

[ApiController, Route("api/open")]
public sealed class ModelController(KeyContext database) : ControllerBase
{
    [HttpGet("model")]
    public async Task<ActionResult<IReadOnlyList<PublicModelResponse>>> GetAvailableModels(CancellationToken token) =>
        Ok(await database.AvailableModels!.AsNoTracking().Select(model => new PublicModelResponse(model.ModelId,
            model.Enable, model.Name, model.ModelValue, model.IsChatModel, model.Vision)).ToListAsync(token));
}
