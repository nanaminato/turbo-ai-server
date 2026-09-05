using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Data.Contexts;
using Turbo.Auth.Models.Suppliers;

namespace Turbo.Auth.Controllers.OpenAPI;
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
        return _keyContext.AvailableModels!.ToList();
    }
    
}