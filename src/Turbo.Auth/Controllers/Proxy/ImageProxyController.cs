using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Security.FeatureAccess;

namespace Turbo.Auth.Controllers.Proxy;

[ApiController]
[Authorize]
[FeatureAccess("image-proxy")]
[Route("api/image_proxy")]
public sealed class ImageProxyController(IConfiguration configuration) : ControllerBase
{
    private const int MaxBytes = 20 * 1024 * 1024;
    [HttpGet("get")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetImage([FromQuery(Name = "url")] string imageUrl)
    {
        var hosts = configuration.GetSection("ImageProxy:AllowedHosts").Get<string[]>() ?? [];
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.Port != 443 || !string.IsNullOrEmpty(uri.UserInfo) ||
            !hosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { code = "image_host_not_allowed" });
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseProxy = false, UseCookies = false,
            ConnectCallback = async (context, token) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
                if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
                    throw new HttpRequestException("Image host must resolve exclusively to public addresses.");
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(addresses[0], context.DnsEndPoint.Port), token);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch { socket.Dispose(); throw; }
            }
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) return StatusCode(502);
            var type = response.Content.Headers.ContentType?.MediaType;
            if (type is not ("image/png" or "image/jpeg" or "image/webp" or "image/gif" or "image/avif"))
                return BadRequest(new { code = "unsupported_image_type" });
            if (response.Content.Headers.ContentLength > MaxBytes) return StatusCode(413);
            await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, timeout.Token)) > 0)
            {
                if (output.Length + read > MaxBytes) return StatusCode(413);
                output.Write(buffer, 0, read);
            }
            Response.Headers.XContentTypeOptions = "nosniff";
            return File(output.ToArray(), type);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { return new EmptyResult(); }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException)
        { return StatusCode(502); }
    }
    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
            return bytes[0] is not (0 or 10 or 127) && bytes[0] < 224 &&
                !(bytes[0] == 169 && bytes[1] == 254) && !(bytes[0] == 172 && bytes[1] is >=16 and <=31) &&
                !(bytes[0] == 192 && bytes[1] == 168) && !(bytes[0] == 100 && bytes[1] is >=64 and <=127) &&
                !(bytes[0] == 192 && bytes[1] == 0) && !(bytes[0] == 198 && bytes[1] is 18 or 19) &&
                !(bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) &&
                !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
        // Only global unicast; exclude documentation, Teredo and 6to4 transition ranges.
        return address.AddressFamily == AddressFamily.InterNetworkV6 && (bytes[0] & 0xe0) == 0x20 &&
            !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8) &&
            !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0 && bytes[3] == 0) &&
            !(bytes[0] == 0x20 && bytes[1] == 0x02);
    }
}
