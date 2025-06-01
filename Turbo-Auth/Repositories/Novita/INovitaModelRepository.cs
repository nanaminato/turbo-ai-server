using iTextSharp.text;
using Turbo_Auth.Models.Ai.Media;

namespace Turbo_Auth.Repositories.Novita;

public interface INovitaModelRepository
{
    Task<List<NovitaModel>> GetVaeModels();
    Task<List<NovitaModel>> GetLoraModels();
    Task<List<NovitaModel>> GetEmbeddingModels();
    Task<List<NovitaModel>> GetImageModels();
    Task<List<NovitaModel>> GetVideoModels();
    
    Task<List<NovitaModel>> GetSafeVaeModels();
    Task<List<NovitaModel>> GetSafeLoraModels();
    Task<List<NovitaModel>> GetSafeEmbeddingModels();
    Task<List<NovitaModel>> GetSafeImageModels();
    Task<List<NovitaModel>> GetSafeVideoModels();
}