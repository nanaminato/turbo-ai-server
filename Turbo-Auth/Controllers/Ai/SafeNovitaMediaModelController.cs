using Microsoft.AspNetCore.Mvc;
using Turbo_Auth.Models.Ai.Media;
using Turbo_Auth.Repositories.Novita;

namespace Turbo_Auth.Controllers.Ai;

[ApiController]
[Route("api/safe-media")]
public class SafeNovitaMediaModelController: Controller
{
    private INovitaModelRepository _repository;
    public SafeNovitaMediaModelController(INovitaModelRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("vae")]
    public async Task<List<NovitaModel>> GetVaeModels()
    {
        return await _repository.GetSafeVaeModels();
    }
    [HttpGet("lora")]
    public async Task<List<NovitaModel>> GetLoraModels()
    {
        return await _repository.GetSafeLoraModels();
    }
    [HttpGet("embedding")]
    public async Task<List<NovitaModel>> GetEmbeddingModels()
    {
        return await _repository.GetSafeEmbeddingModels();
    }
    [HttpGet("image")]
    public async Task<List<NovitaModel>> GetImageModels()
    {
        return await _repository.GetSafeImageModels();
    }

    [HttpGet("video")]
    public async Task<List<NovitaModel>> GetVideoModels()
    {
        return await _repository.GetSafeVideoModels();
    }
    
}