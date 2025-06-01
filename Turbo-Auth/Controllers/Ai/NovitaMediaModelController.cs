using Microsoft.AspNetCore.Mvc;
using Turbo_Auth.Models.Ai.Media;
using Turbo_Auth.Repositories.Novita;

namespace Turbo_Auth.Controllers.Ai;
[ApiController]
[Route("api/media")]
public class NovitaMediaModelController: Controller
{
    private INovitaModelRepository _repository;
    public NovitaMediaModelController(INovitaModelRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("vae")]
    public async Task<List<NovitaModel>> GetVaeModels()
    {
        return await _repository.GetVaeModels();
    }
    [HttpGet("lora")]
    public async Task<List<NovitaModel>> GetLoraModels()
    {
        return await _repository.GetLoraModels();
    }
    [HttpGet("embedding")]
    public async Task<List<NovitaModel>> GetEmbeddingModels()
    {
        return await _repository.GetEmbeddingModels();
    }
    [HttpGet("image")]
    public async Task<List<NovitaModel>> GetImageModels()
    {
        return await _repository.GetImageModels();
    }

    [HttpGet("video")]
    public async Task<List<NovitaModel>> GetVideoModels()
    {
        return await _repository.GetVideoModels();
    }
    
}