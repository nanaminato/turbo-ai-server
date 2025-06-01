using Microsoft.EntityFrameworkCore;
using Turbo_Auth.Context;
using Turbo_Auth.Models.Ai.Media;

namespace Turbo_Auth.Repositories.Novita;

public class NovitaModelRepository: INovitaModelRepository
{
    private KeyContext _context;
    public NovitaModelRepository(KeyContext context)
    {
        _context = context;
    }

    public async Task<List<NovitaModel>> GetVaeModels()
    {
        return await _context.NovitaModels!.Where(m => m.Type == "vae")
            .ToListAsync();
    }

    public async Task<List<NovitaModel>> GetLoraModels()
    {
        return await _context.NovitaModels!.Where(m => m.Type == "lora")
            .ToListAsync();
    }

    public async Task<List<NovitaModel>> GetEmbeddingModels()
    {
        return await _context.NovitaModels!.Where(m => m.Type == "textualinversion")
            .ToListAsync();
    }

    public async Task<List<NovitaModel>> GetImageModels()
    {
        return await _context.NovitaModels!.Where(m => m.Type == "checkpoint")
            .ToListAsync();
    }

    public async Task<List<NovitaModel>> GetVideoModels()
    {
        return await _context.NovitaModels!.Where(m => m.Type == "video")
            .ToListAsync();
    }

    public async Task<List<NovitaModel>> GetSafeVaeModels()
    {
        return await _context.NovitaModels!.Where(m => m.Type == "vae"&&m.Nsfw==false)
            .ToListAsync();
    }

    public async Task<List<NovitaModel>> GetSafeLoraModels()
    {
        return await _context.NovitaModels!.Where(m => m.Type == "lora"&&m.Nsfw==false)
            .ToListAsync();
    }

    public async Task<List<NovitaModel>> GetSafeEmbeddingModels()
    {
        return await _context.NovitaModels!.Where(m => m.Type == "textualinversion"&&m.Nsfw==false)
            .ToListAsync();
    }

    public async Task<List<NovitaModel>> GetSafeImageModels()
    {
        return await _context.NovitaModels!.Where(m => m.Type == "checkpoint"&&m.Nsfw==false)
            .ToListAsync();
    }

    public async Task<List<NovitaModel>> GetSafeVideoModels()
    {
        return await _context.NovitaModels!.Where(m => m.Type == "video"&&m.Nsfw==false)
            .ToListAsync();
    }
}