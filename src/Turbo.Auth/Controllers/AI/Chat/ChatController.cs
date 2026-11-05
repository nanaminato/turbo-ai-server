using Turbo.Auth.Security.FeatureAccess;
using Turbo.Auth.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Contracts.Chat;
using Turbo.Auth.Application.Chat;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Models.AI.Chat;
using Turbo.Auth.Repositories.Catalog;

namespace Turbo.Auth.Controllers.AI.Chat;

[Authorize]
[ApiController]
[Route("api/ai")]
public class ChatController : Controller
{
    private readonly IChatService _chat;
    private readonly QuickModel _quickModel;
    private readonly IModelRepository _modelRepository;
    private readonly ILogger<ChatController> _logger;
    public ChatController(IChatService chat, QuickModel quickModel,
        IModelRepository modelRepository, ILogger<ChatController> logger)
    {
        _chat = chat;
        _quickModel = quickModel;
        _modelRepository = modelRepository;
        _logger = logger;
    }

    [FeatureAccess("chat")]
    [HttpPost("chat")]
    public async Task Chat(ChatCompletionRequest chatBody)
    {
        var started = false;
        var framed = Request.Headers.Accept.ToString().Split(',').Any(value => value.Trim().StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase));
        try
        {
            if (string.IsNullOrWhiteSpace(chatBody.Model))
            {
                await WriteProblem(400, ApiMessages.Get("model_required"));
                return;
            }
            await foreach (var chunk in _chat.Stream(chatBody.ToApplication(FeatureIdentity.AccountId(User)), HttpContext.RequestAborted))
            {
                started = true;
                if (framed) await ChatStreamWriter.Write(Response, "delta", HttpContext.RequestAborted, text: chunk);
                else
                {
                    if (!Response.HasStarted) Response.ContentType = "text/plain; charset=utf-8";
                    await Response.WriteAsync(chunk, HttpContext.RequestAborted);
                    await Response.Body.FlushAsync(HttpContext.RequestAborted);
                }
            }
            if (framed) await ChatStreamWriter.Write(Response, "completed", HttpContext.RequestAborted);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            HttpContext.Abort();
        }
        catch (Exception exception)
        {
            _logger.LogError("AI chat failed. TraceId: {TraceId}; ExceptionType: {ExceptionType}",
                HttpContext.TraceIdentifier, exception.GetType().Name);
            if (started || Response.HasStarted)
            {
                if (framed) await ChatStreamWriter.Write(Response, "error", HttpContext.RequestAborted, code: "ai_unavailable");
                else HttpContext.Abort();
                return;
            }
            var (status, code) = exception switch
            {
                KeyNotFoundException => (400, "model_unavailable"),
                NotSupportedException => (400, "chat_unsupported"),
                _ => (502, "ai_unavailable")
            };
            await WriteProblem(status, ApiMessages.Get(code));
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
    public async Task<ActionResult<List<ChatModelResponse>>> GetChatModels()
    {
        var models = await _modelRepository.GetChatModelsAsync();
        return Ok((models ?? [])
            .Where(model => _quickModel.IsModelAvailable(model.ModelValue))
            .Select(m => new ChatModelResponse(m.Name, m.ModelValue, m.Vision)).ToList());
    }
}
