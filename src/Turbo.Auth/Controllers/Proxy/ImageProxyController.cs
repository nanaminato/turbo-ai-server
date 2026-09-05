using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace Turbo.Auth.Controllers.Proxy;

[ApiController]
[Route("api/image_proxy")]
public class ImageProxyController: Controller
{
    private readonly ILogger<ImageProxyController> _logger;
    private readonly IDistributedCache _cache;
    
    public ImageProxyController(
        ILogger<ImageProxyController> logger,
        IHttpClientFactory httpClientFactory,
        IDistributedCache cache
        )
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }
    
    private readonly IHttpClientFactory _httpClientFactory;

    [HttpGet("get")]
    [ResponseCache(Duration = 2592000, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetImage([FromQuery(Name = "url")]string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return BadRequest();

        // 缓存 Key
        var cacheKey = $"img:{imageUrl}";

        // 1. 尝试从分布式缓存获取
        var cachedData = await _cache.GetAsync(cacheKey);
        if (cachedData != null)
        {
            // 解析缓存数据 (我们采用：前100字节存 ContentType，后面存图片内容)
            var (contentType, imageBytes) = UnpackCache(cachedData);
            var etag = $"\"{imageUrl}\""; 
            if (Request.Headers.IfNoneMatch == etag) return StatusCode(304);

            Response.Headers.ETag = etag;
            return File(imageBytes, contentType);
        }

        // 2. 缓存未命中，发起请求
        var client = _httpClientFactory.CreateClient();
        var targetUrl = $"{imageUrl}";

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
            var etag = $"\"{imageUrl}\""; 
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