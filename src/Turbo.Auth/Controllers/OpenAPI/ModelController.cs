using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turbo.Auth.Contracts.Catalog;
using Turbo.Auth.Data.Contexts;

namespace Turbo.Auth.Controllers.OpenAPI;

[ApiController, Route("api/open")]
public sealed class ModelController(KeyContext database) : ControllerBase
{
    [HttpGet("model")]
    public async Task<ActionResult<IReadOnlyList<PublicModelResponse>>> GetAvailableModels(CancellationToken token) =>
        Ok(await database.AvailableModels!.AsNoTracking().Select(model => new PublicModelResponse(model.ModelId,
            model.Enable, model.Name, model.ModelValue, model.IsChatModel, model.Vision)).ToListAsync(token));
}
