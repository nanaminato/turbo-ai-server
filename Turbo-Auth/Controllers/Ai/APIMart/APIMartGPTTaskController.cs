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
    private readonly ILogger<APIMartGPTTaskController> _logger;
    private readonly IDistributedCache _cache;
    
    public APIMartGPTTaskController(
        QuickModel quickModel,
        ILogger<APIMartGPTTaskController> logger,
        IHttpClientFactory httpClientFactory,
        IDistributedCache cache
        )
    {
        _quickModel = quickModel;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
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
    private readonly IHttpClientFactory _httpClientFactory;

    [HttpGet("f/image/{*image_id}")]
    [ResponseCache(Duration = 2592000, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetImage(string image_id)
    {
        if (string.IsNullOrEmpty(image_id)) return BadRequest();

        // 缓存 Key
        var cacheKey = $"img:{image_id}";

        // 1. 尝试从分布式缓存获取
        var cachedData = await _cache.GetAsync(cacheKey);
        if (cachedData != null)
        {
            // 解析缓存数据 (我们采用：前100字节存 ContentType，后面存图片内容)
            var (contentType, imageBytes) = UnpackCache(cachedData);
            var etag = $"\"{image_id}\""; 
            if (Request.Headers.IfNoneMatch == etag) return StatusCode(304);

            Response.Headers.ETag = etag;
            return File(imageBytes, contentType);
        }

        // 2. 缓存未命中，发起请求
        var client = _httpClientFactory.CreateClient();
        var targetUrl = $"https://upload.apimart.ai/f/image/{image_id}";

        try
        {
            var response = await client.GetAsync(targetUrl);
            if (!response.IsSuccessStatusCode) return StatusCode((int)response.StatusCode);

            var imageBytes = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";

            // 3. 打包并存入分布式缓存
            var dataToCache = PackCache(contentType, imageBytes);
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7), // 7天过期
                SlidingExpiration = TimeSpan.FromDays(1) // 1天内无人访问则失效
            };

            await _cache.SetAsync(cacheKey, dataToCache, cacheOptions);
            var etag = $"\"{image_id}\""; 
            if (Request.Headers.IfNoneMatch == etag) return StatusCode(304);

            Response.Headers.ETag = etag;
            return File(imageBytes, contentType);
        }
        catch (Exception)
        {
            return StatusCode(502);
        }
    }

    // --- 辅助方法：处理 IDistributedCache 只能存 byte[] 的限制 ---

    private byte[] PackCache(string contentType, byte[] imageBytes)
    {
        // 简单的协议：前128字节存字符串，后面存数据
        var header = new byte[128];
        var typeBytes = System.Text.Encoding.UTF8.GetBytes(contentType);
        Buffer.BlockCopy(typeBytes, 0, header, 0, Math.Min(typeBytes.Length, 128));
        
        var combined = new byte[header.Length + imageBytes.Length];
        Buffer.BlockCopy(header, 0, combined, 0, header.Length);
        Buffer.BlockCopy(imageBytes, 0, combined, header.Length, imageBytes.Length);
        return combined;
    }

    private (string contentType, byte[] imageBytes) UnpackCache(byte[] cachedData)
    {
        var header = new byte[128];
        Buffer.BlockCopy(cachedData, 0, header, 0, 128);
        var contentType = System.Text.Encoding.UTF8.GetString(header).TrimEnd('\0');

        var imageBytes = new byte[cachedData.Length - 128];
        Buffer.BlockCopy(cachedData, 128, imageBytes, 0, imageBytes.Length);
        
        return (contentType, imageBytes);
    }

}