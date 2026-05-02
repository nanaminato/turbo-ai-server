using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Ai.Image.Request.APIMart;
using Turbo_Auth.Models.Ai.Image.Response.APIMartTask;

namespace Turbo_Auth.Controllers.Ai;

[ApiController]
[Authorize("vip")]
[Route("api/apimart/image-generate")]
public class APIMartGPTImageController: Controller
{
    private QuickModel _quickModel;
    private readonly ILogger<APIMartGPTImageController> _logger;

    public APIMartGPTImageController(
        QuickModel quickModel,
        ILogger<APIMartGPTImageController> logger
    )
    {
        _quickModel = quickModel;
        _logger = logger;
    }

    [HttpPost("gpt-image-2")]
    public async Task<IActionResult> GPTImage2Generate(APIMartGPTImage2Request request)
    {
        var modelKeys = _quickModel.GetModelKeys(request.Model);
        var modelKey = modelKeys.FirstOrDefault(m=>m.SupplierKey!.BaseUrl!.Contains("apimart"));
        if (modelKey == null)
        {
            return BadRequest("no api key is available");
        }
        var url = modelKey.SupplierKey!.BaseUrl+"/v1/images/generations";
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer "+modelKey.SupplierKey.ApiKey);
        var payload = JsonConvert.SerializeObject(request);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, content);
        var result = await response.Content.ReadAsStringAsync();
        var task = JsonConvert.DeserializeObject<APIMartGeneratetTask>(result);
        return Ok(task);
    }
    [HttpPost("gpt-image-2-official")]
    public async Task<IActionResult> GPTImage2OfficialGenerate(APIMartGPTImage2OfficialRequest request)
    {
        var modelKeys = _quickModel.GetModelKeys(request.Model!);
        var modelKey = modelKeys.FirstOrDefault(m=>m.SupplierKey!.BaseUrl!.Contains("apimart"));
        if (modelKey == null)
        {
            return BadRequest("no api key is available");
        }
        var url = modelKey.SupplierKey!.BaseUrl+"/v1/images/generations";
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer "+modelKey.SupplierKey.ApiKey);
        var payload = JsonConvert.SerializeObject(request);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, content);
        var result = await response.Content.ReadAsStringAsync();
        var task = JsonConvert.DeserializeObject<APIMartGeneratetTask>(result);
        return Ok(task);
    }
}