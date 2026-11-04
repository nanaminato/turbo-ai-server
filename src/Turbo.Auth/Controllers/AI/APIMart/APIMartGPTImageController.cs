using Turbo.Auth.Security.FeatureAccess;
using Turbo.Auth.Localization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Turbo.Auth.Controllers.AI.APIMart.Models;
using Turbo.Auth.Application.Routing;

namespace Turbo.Auth.Controllers.AI.APIMart;

[FeatureAccess("image")]
[ApiController]
[Authorize]
[Route("api/apimart/image-generate")]
public class APIMartGPTImageController: Controller
{
    private readonly FeatureAccessStore _access;
    private QuickModel _quickModel;
    private readonly ILogger<APIMartGPTImageController> _logger;

    public APIMartGPTImageController(
        QuickModel quickModel,
        FeatureAccessStore access,
        ILogger<APIMartGPTImageController> logger
    )
    {
        _quickModel = quickModel;
        _access = access;
        _logger = logger;
    }

    [HttpPost("gpt-image-2")]
    public async Task<IActionResult> GPTImage2Generate(APIMartGPTImage2Request request)
    {
        var apiMartKey = _quickModel.GetApiMartKey();
        if (apiMartKey==null)
        {
            return BadRequest(ApiMessages.Get("no_api_key"));
        }
        var url = apiMartKey!.BaseUrl+"/v1/images/generations";
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer "+apiMartKey.ApiKey);
        var payload = JsonConvert.SerializeObject(request);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, content, HttpContext.RequestAborted);
        var result = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);
        var task = JsonConvert.DeserializeObject<APIMartGeneratetTask>(result);
        if (response.IsSuccessStatusCode && task?.Data is not null)
            foreach (var item in task.Data.Where(item => !string.IsNullOrWhiteSpace(item.TaskId)))
                await _access.RecordTask(FeatureIdentity.AccountId(User)!.Value, item.TaskId, HttpContext.RequestAborted);
        return Ok(task);
    }
    [HttpPost("gpt-image-2-official")]
    public async Task<IActionResult> GPTImage2OfficialGenerate(APIMartGPTImage2OfficialRequest request)
    {
        var apiMartKey = _quickModel.GetApiMartKey();
        if (apiMartKey==null)
        {
            return BadRequest(ApiMessages.Get("no_api_key"));
        }
        var url = apiMartKey!.BaseUrl+"/v1/images/generations";
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer "+apiMartKey.ApiKey);
        var payload = JsonConvert.SerializeObject(request);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, content, HttpContext.RequestAborted);
        var result = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);
        var task = JsonConvert.DeserializeObject<APIMartGeneratetTask>(result);
        if (response.IsSuccessStatusCode && task?.Data is not null)
            foreach (var item in task.Data.Where(item => !string.IsNullOrWhiteSpace(item.TaskId)))
                await _access.RecordTask(FeatureIdentity.AccountId(User)!.Value, item.TaskId, HttpContext.RequestAborted);
        return Ok(task);
    }
}