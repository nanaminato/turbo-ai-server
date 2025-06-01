using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo_Auth.Handlers.Chat;
using Turbo_Auth.Handlers.Differentiator;
using Turbo_Auth.Handlers.Model;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Ai.Chat;
using Turbo_Auth.Repositories.ApiAssets;

namespace Turbo_Auth.Controllers.Ai;
[Authorize(Policy = "vip")]
[ApiController]
[Route("api/ai")]
public class ChatController: Controller
{
    private IChatHandlerObtain _chatHandlerObtain;
    private QuickModel _quickModel;
    private PlayMixModelBacker _backer;
    private IModelRepository _modelRepository;
    public ChatController(IChatHandlerObtain chatHandlerObtain, 
        QuickModel quickModel,PlayMixModelBacker backer,
        IModelRepository modelRepository
    )
    {
        _chatHandlerObtain = chatHandlerObtain;
        _quickModel = quickModel;
        _backer = backer;
        _modelRepository = modelRepository; 
    }
    
    
    [HttpPost("chat")]
    public async Task Chat(NoModelChatBody chatBody)
    {
        try
        {
            var modelKey = _quickModel.GetModelAndKey(chatBody.Model!);
            modelKey!.Model = _backer.Backer(modelKey.Model!);
            var handler = _chatHandlerObtain.GetHandler
                ((HandlerType)modelKey!.SupplierKey!.RequestIdentifier);
            await handler.Chat(chatBody,modelKey,Response);
        }
        catch (Exception e)
        {
            await Response.WriteAsync(e.Message);
            await Response.CompleteAsync();
        }
    }

    [HttpGet("models")]
    [Authorize("user")]
    public async Task<List<ChatDisplayModel>> GetChatModels()
    {
        var models = await _modelRepository.GetChatModelsAsync();
        return models!.Select(m=>new ChatDisplayModel()
        {
            ModelName = m.Name,
            ModelValue = m.ModelValue,
            Vision = m.Vision
        }).ToList();
    }
}