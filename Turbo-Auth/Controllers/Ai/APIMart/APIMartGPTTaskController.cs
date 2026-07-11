using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using Turbo_Auth.Controllers.Ai.APIMart.Models;
using Turbo_Auth.Handlers.Model2Key;

namespace Turbo_Auth.Controllers.Ai.APIMart;
[ApiController]
[Route("api/apimart")]
public class APIMartGPTTaskController: Controller
{
    private QuickModel _quickModel;
    
    public APIMartGPTTaskController(
        QuickModel quickModel
        )
    {
        _quickModel = quickModel;
    }
    [Authorize("vip")]
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