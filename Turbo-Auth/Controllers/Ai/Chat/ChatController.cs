using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo_Auth.Controllers.Ai.Chat.Models;
using Turbo_Auth.Handlers.Chat;
using Turbo_Auth.Handlers.Differentiator;
using Turbo_Auth.Handlers.Model;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Ai.Chat;
using Turbo_Auth.Repositories.ApiAssets;

namespace Turbo_Auth.Controllers.Ai.Chat;
[Authorize(Policy = "vip")]
[ApiController]
[Route("api/ai")]
public class ChatController: Controller
{
    private IChatHandlerObtain _chatHandlerObtain;
    private QuickModel _quickModel;
    private PlayMixModelBacker _backer;
    private IModelRepository _modelRepository;
    private readonly ILogger<ChatController> _logger;
    public ChatController(IChatHandlerObtain chatHandlerObtain, 
        QuickModel quickModel,PlayMixModelBacker backer,
        IModelRepository modelRepository, ILogger<ChatController> logger
    )
    {
        _chatHandlerObtain = chatHandlerObtain;
        _quickModel = quickModel;
        _backer = backer;
        _modelRepository = modelRepository; 
        _logger = logger;
    }
    
    
    [HttpPost("chat")]
    public async Task Chat(NoModelChatBody chatBody)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(chatBody.Model))
            {
                await WriteProblem(StatusCodes.Status400BadRequest, "必须指定 model。");
                return;
            }

            var modelKey = _quickModel.GetModelAndKey(chatBody.Model);
            modelKey.Model = _backer.Backer(modelKey.Model!);
            var handler = _chatHandlerObtain.GetHandler
                ((HandlerType)modelKey.SupplierKey!.RequestIdentifier);
            await handler.Chat(chatBody, modelKey, Response, HttpContext.RequestAborted);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("AI chat request cancelled. TraceId: {TraceId}", HttpContext.TraceIdentifier);
        }
        catch (KeyNotFoundException exception)
        {
            _logger.LogWarning(exception, "AI chat model is unavailable. TraceId: {TraceId}", HttpContext.TraceIdentifier);
            await WriteProblem(StatusCodes.Status400BadRequest, "请求的模型当前不可用。");
        }
        catch (NotSupportedException exception)
        {
            _logger.LogWarning(exception, "AI chat provider is unsupported. TraceId: {TraceId}", HttpContext.TraceIdentifier);
            await WriteProblem(StatusCodes.Status400BadRequest, "该模型的供应商暂不支持聊天。");
        }
        catch (Exception e)
        {
            _logger.LogError(
                "AI chat request failed. TraceId: {TraceId}; ExceptionType: {ExceptionType}",
                HttpContext.TraceIdentifier,
                e.GetType().Name);
            await WriteProblem(StatusCodes.Status500InternalServerError, "AI 服务暂时不可用。");
        }
    }

    private async Task WriteProblem(int statusCode, string title)
    {
        if (Response.HasStarted)
        {
            return;
        }

        Response.StatusCode = statusCode;
        Response.ContentType = "application/problem+json";
        await Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Instance = HttpContext.Request.Path
        });
        await Response.CompleteAsync();
    }

    [HttpGet("models")]
    [Authorize("user")]
    public async Task<List<ChatDisplayModel>> GetChatModels()
    {
        var models = await _modelRepository.GetChatModelsAsync();
        return (models ?? [])
            .Where(model => _quickModel.IsModelAvailable(model.ModelValue))
            .Select(m=>new ChatDisplayModel()
        {
            ModelName = m.Name,
            ModelValue = m.ModelValue,
            Vision = m.Vision
        }).ToList();
    }
}
