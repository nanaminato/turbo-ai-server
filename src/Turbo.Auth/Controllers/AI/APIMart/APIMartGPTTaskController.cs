using Turbo.Auth.Security.FeatureAccess;
using Turbo.Auth.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using Turbo.Auth.Contracts.Media;
using Turbo.Auth.Contracts.Common;
using Turbo.Auth.Application.Routing;

namespace Turbo.Auth.Controllers.AI.APIMart;

[FeatureAccess("image-task")]
[ApiController]
[Route("api/apimart")]
public class APIMartGPTTaskController : Controller
{
    private readonly FeatureAccessStore _access;
    private readonly IHttpClientFactory _http;
    private QuickModel _quickModel;

    public APIMartGPTTaskController(
        QuickModel quickModel, FeatureAccessStore access, IHttpClientFactory http
        )
    {
        _quickModel = quickModel;
        _access = access;
        _http = http;
    }
    [Authorize]
    [HttpGet("getTask/{task_id}")]
    public async Task<ActionResult<TaskResponse>> GPTImage2OfficialGenerate(string task_id, string language)
    {
        if (task_id.Length > 255 || !await _access.OwnsTask(FeatureIdentity.AccountId(User)!.Value, task_id, HttpContext.RequestAborted))
            return NotFound();
        var apiMartKey = _quickModel.GetApiMartKey();
        if (apiMartKey == null)
        {
            return BadRequest(ApiMessages.Get("no_api_key"));
        }
        var url = apiMartKey!.BaseUrl + $"/v1/tasks/{Uri.EscapeDataString(task_id)}?language={Uri.EscapeDataString(language ?? "en")}";
        using var client = _http.CreateClient("AiProvider");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiMartKey.ApiKey);

        using var response = await client.GetAsync(url, HttpContext.RequestAborted);
        var result = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);
        if (!response.IsSuccessStatusCode) return StatusCode(502, new CodeResponse("ai_unavailable"));
        var task = JsonConvert.DeserializeObject<TaskResponse>(result);
        return Ok(task);
    }



}