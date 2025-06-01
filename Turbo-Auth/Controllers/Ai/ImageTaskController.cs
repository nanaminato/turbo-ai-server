using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Ai.Image.Response.Task;

namespace Turbo_Auth.Controllers.Ai;

[Route("api/[controller]")]
[ApiController]
public class ImageTaskController: Controller
{
    private QuickModel _quickModel;
    private string Key => _quickModel.GetNovitaKey().ApiKey!;

    public ImageTaskController(
        QuickModel quickModel)
    {
        _quickModel = quickModel;
    }

    [HttpGet("{id}")]
    public async Task<TaskFinalResult?> GetTaskResult(string id)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.novita.ai/v3/async/task-result?task_id={id}");
        request.Headers.Add("Authorization", $"Bearer {Key}");
        var response = await client.SendAsync(request);
        var res = await response.Content.ReadAsStringAsync();
        Console.WriteLine(res);
        return JsonConvert.DeserializeObject<TaskFinalResult>(res);
    }
}