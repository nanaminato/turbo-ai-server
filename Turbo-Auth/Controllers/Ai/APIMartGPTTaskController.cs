using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Ai.Image.Response.APIMartTask;

namespace Turbo_Auth.Controllers.Ai;
[ApiController]
[Authorize("vip")]
[Route("api/apimart")]
public class APIMartGPTTaskController: Controller
{
    private QuickModel _quickModel;
    private readonly ILogger<APIMartGPTTaskController> _logger;

    public APIMartGPTTaskController(
        QuickModel quickModel,
        ILogger<APIMartGPTTaskController> logger
    )
    {
        _quickModel = quickModel;
        _logger = logger;
    }
    [HttpGet("getTask/{task_id}")]
    public async Task<IActionResult> GPTImage2OfficialGenerate(string task_id, string language)
    {
        var apiMartKey = _quickModel.GetApiMartKey();
        if (apiMartKey==null)
        {
            return BadRequest("no api key is available");
        }
        var url = apiMartKey!.BaseUrl+$"/v1/tasks/{task_id}?language={language}";
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer "+apiMartKey.ApiKey);
        
        var response = await client.GetAsync(url);
        var result = await response.Content.ReadAsStringAsync();
        var task = JsonConvert.DeserializeObject<TaskResponse>(result);
        return Ok(task);
    }
}