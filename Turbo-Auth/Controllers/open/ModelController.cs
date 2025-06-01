using Microsoft.AspNetCore.Mvc;
using Turbo_Auth.Context;
using Turbo_Auth.Models.Suppliers;
namespace Turbo_Auth.Controllers.open;
[ApiController]
[Route("api/open")]
public class ModelController
{
    private KeyContext _keyContext;

    public ModelController(KeyContext keyContext)
    {
        _keyContext = keyContext;
    }
    [HttpGet("model")]
    public List<AvailableModel> GetAvailableModels()
    {
        return this._keyContext.AvailableModels.ToList();
    }
    
}