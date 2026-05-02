using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Ai.Image.Response.APIMartTask;
using Turbo_Auth.Models.Suppliers;

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
        var weightKeys = _quickModel.GetQuick();
        SupplierKey? supplierKey = null;
        foreach (var key in weightKeys.Select(pair => pair.Value.FirstOrDefault(x => x.SupplierKey!.BaseUrl!.Contains("apimart"))).OfType<WeightKey>())
        {
            supplierKey = key.SupplierKey!;
        }
        if (supplierKey == null)
        {
            return BadRequest("no api key is available");
        }
        var url = supplierKey!.BaseUrl+$"/v1/tasks/{task_id}?language={language}";
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer "+supplierKey.ApiKey);
        
        var response = await client.GetAsync(url);
        var result = await response.Content.ReadAsStringAsync();
        var task = JsonConvert.DeserializeObject<TaskResponse>(result);
        return Ok(task);
    }
}