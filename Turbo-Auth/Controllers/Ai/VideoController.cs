using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Ai.Media;
using Turbo_Auth.Models.Ai.Video.Request;

namespace Turbo_Auth.Controllers.Ai;

[ApiController]
[Authorize("vip")]
[Route("api/[controller]")]
public class VideoController
{
    private QuickModel _quickModel;
    private string Key => _quickModel.GetNovitaKey().ApiKey!;
    public VideoController(
        QuickModel quickModel)
    {
        _quickModel = quickModel;
    }

    [HttpPost("text2video")]
    public async Task<AsyncGenerateTask?> TextToVideo(Text2Video body)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.novita.ai/v3/async/txt2video");
        request.Headers.Add("Authorization", $"Bearer {Key}");
        var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
        request.Content = content;
        var response = await client.SendAsync(request);
        // response.EnsureSuccessStatusCode();
        var resText = await response.Content.ReadAsStringAsync();
        Console.WriteLine(resText);
        return JsonConvert.DeserializeObject<AsyncGenerateTask?>(resText);
    }
}