using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Ai.Image.Request;
using Turbo_Auth.Models.Ai.Image.Response.Lcm;
using Turbo_Auth.Models.Ai.Media;

namespace Turbo_Auth.Controllers.Ai;

[Authorize(Policy = "vip")]
[ApiController]
[Route("api/[controller]")]
public class ImageController : Controller
{
    
    private QuickModel _quickModel;
    private string Key => _quickModel.GetNovitaKey().ApiKey!;
    public ImageController(
        QuickModel quickModel)
    {
        _quickModel = quickModel;
    }
    
    [HttpPost("text2-lcm")]
    public async Task<Text2LcmResponse?> NovitaText2Lcm(Text2Lcm body)
    {
        Console.WriteLine(_quickModel);
        var client = new HttpClient();
        // Console.WriteLine(body);
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.novita.ai/v3/lcm-txt2img");
        request.Headers.Add("Authorization", $"Bearer {Key}");
        // request.Headers.Add("Accept-Encoding", "gzip");
        var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
        request.Content = content;
        var response = await client.SendAsync(request);
        var resText = await response.Content.ReadAsStringAsync();
        // response.EnsureSuccessStatusCode();
        // Console.WriteLine(resText);
        return JsonConvert.DeserializeObject<Text2LcmResponse?>(resText);
    }

    [HttpPost("img2-lcm")]
    public async Task<Image2LcmResponse?> NovitaImage2Lcm(Image2Lcm body)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.novita.ai/v3/lcm-img2img");
        request.Headers.Add("Authorization", $"Bearer {Key}");
        var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
        request.Content = content;
        var response = await client.SendAsync(request);
        var resText = await response.Content.ReadAsStringAsync();
        // response.EnsureSuccessStatusCode();
        // Console.WriteLine(resText);
        return JsonConvert.DeserializeObject<Image2LcmResponse>(resText);
    }

    [HttpPost("text2")]
    public async Task<AsyncGenerateTask?> NovitaText2(Text2 body)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.novita.ai/v3/async/txt2img");
        request.Headers.Add("Authorization", $"Bearer {Key}");
        var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
        request.Content = content;
        var response = await client.SendAsync(request);
        var resText = await response.Content.ReadAsStringAsync();
        Console.WriteLine(resText);
        return JsonConvert.DeserializeObject<AsyncGenerateTask?>(resText);
    }
    [HttpPost("img2")]
    public async Task<AsyncGenerateTask?> NovitaImg2(Image2 body)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.novita.ai/v3/async/img2img");
        request.Headers.Add("Authorization", $"Bearer {Key}");
        var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
        request.Content = content;
        var response = await client.SendAsync(request);
        var resText = await response.Content.ReadAsStringAsync();
        Console.WriteLine(resText);
        return JsonConvert.DeserializeObject<AsyncGenerateTask?>(resText);
    }
}

class ModelsExtractor
{
    public NovitaExtractor[]? Models { get; set; }
}

class NovitaExtractor
{
    [JsonProperty("sd_name_in_api")] 
    public string? SdNameInApi { get; set; }

    [JsonProperty("cover_url")]
    public string? Cover
    {
        get;
        set;
    }

    [JsonProperty("type")]
    public ModelType? ModelType
    {
        get;
        set;
    }
    [JsonProperty("is_nsfw")]
    public bool IsNsfw
    {
        get;
        set;
    }
    [JsonProperty("is_sdxl")]
    public bool IsSdxl
    {
        get;
        set;
    }
}

public class ModelType
{
    [JsonProperty("name")]
    public string? Name
    {
        get;
        set;
    }
    [JsonProperty("display_name")]
    public string? DisplayName
    {
        get;
        set;
    }
}