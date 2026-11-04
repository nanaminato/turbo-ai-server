using Turbo.Auth.Security.FeatureAccess;
using Turbo.Auth.Localization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Turbo.Auth.Contracts.Media;
using Turbo.Auth.Contracts.Common;
using Turbo.Auth.Application.Routing;

namespace Turbo.Auth.Controllers.AI.APIMart;

[FeatureAccess("image")]
[ApiController]
[Authorize]
[Route("api/apimart/image-generate")]
public class APIMartGPTImageController : Controller
{
    private readonly FeatureAccessStore _access;
    private readonly IHttpClientFactory _http;
    private QuickModel _quickModel;
    private readonly ILogger<APIMartGPTImageController> _logger;

    public APIMartGPTImageController(
        QuickModel quickModel,
        FeatureAccessStore access, IHttpClientFactory http,
        ILogger<APIMartGPTImageController> logger
    )
    {
        _quickModel = quickModel;
        _access = access;
        _http = http;
        _logger = logger;
    }

    [HttpPost("gpt-image-2")]
    public async Task<ActionResult<APIMartGenerationResponse>> GPTImage2Generate(APIMartGPTImage2Request request)
    {
        var apiMartKey = _quickModel.GetApiMartKey();
        if (apiMartKey == null)
        {
            return BadRequest(ApiMessages.Get("no_api_key"));
        }
        var url = apiMartKey!.BaseUrl + "/v1/images/generations";
        using var client = _http.CreateClient("AiProvider");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiMartKey.ApiKey);
        var payload = JsonConvert.SerializeObject(request);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(url, content, HttpContext.RequestAborted);
        var result = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);
        if (!response.IsSuccessStatusCode) return StatusCode(502, new CodeResponse("ai_unavailable"));
        var task = JsonConvert.DeserializeObject<APIMartGenerationResponse>(result);
        if (response.IsSuccessStatusCode && task?.Data is not null)
            foreach (var item in task.Data.Where(item => !string.IsNullOrWhiteSpace(item.TaskId)))
                await _access.RecordTask(FeatureIdentity.AccountId(User)!.Value, item.TaskId, HttpContext.RequestAborted);
        return Ok(task);
    }
    [HttpPost("gpt-image-2-official")]
    public async Task<ActionResult<APIMartGenerationResponse>> GPTImage2OfficialGenerate(APIMartGPTImage2OfficialRequest request)
    {
        var apiMartKey = _quickModel.GetApiMartKey();
        if (apiMartKey == null)
        {
            return BadRequest(ApiMessages.Get("no_api_key"));
        }
        var url = apiMartKey!.BaseUrl + "/v1/images/generations";
        using var client = _http.CreateClient("AiProvider");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiMartKey.ApiKey);
        var payload = JsonConvert.SerializeObject(request);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(url, content, HttpContext.RequestAborted);
        var result = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);
        if (!response.IsSuccessStatusCode) return StatusCode(502, new CodeResponse("ai_unavailable"));
        var task = JsonConvert.DeserializeObject<APIMartGenerationResponse>(result);
        if (response.IsSuccessStatusCode && task?.Data is not null)
            foreach (var item in task.Data.Where(item => !string.IsNullOrWhiteSpace(item.TaskId)))
                await _access.RecordTask(FeatureIdentity.AccountId(User)!.Value, item.TaskId, HttpContext.RequestAborted);
        return Ok(task);
    }
}