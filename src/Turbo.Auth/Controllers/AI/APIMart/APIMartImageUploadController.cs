using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Contracts.Common;
using Turbo.Auth.Security.FeatureAccess;

namespace Turbo.Auth.Controllers.AI.APIMart;

[ApiController, Authorize, FeatureAccess("image"), Route("api/apimart/uploads/images")]
public sealed class APIMartImageUploadController(QuickModel routes, IHttpClientFactory http) : ControllerBase
{
    public const long MaximumBytes = 20 * 1024 * 1024;
    public sealed record UploadResponse(string Url);

    [HttpPost, RequestSizeLimit(MaximumBytes + 65536), RequestFormLimits(MultipartBodyLengthLimit = MaximumBytes + 65536)]
    public async Task<ActionResult<UploadResponse>> Upload(IFormFile file, CancellationToken token)
    {
        if (file.Length is <= 0 or > MaximumBytes) return BadRequest(new CodeResponse("invalid_image_upload"));
        var mime = file.ContentType.ToLowerInvariant();
        if (mime is not ("image/png" or "image/jpeg" or "image/webp" or "image/gif"))
            return BadRequest(new CodeResponse("invalid_image_upload"));
        await using var stream = file.OpenReadStream();
        if (!stream.CanSeek) return BadRequest(new CodeResponse("invalid_image_upload"));
        var prefix = new byte[12];
        var length = await stream.ReadAtLeastAsync(prefix, 12, throwOnEndOfStream: false, cancellationToken: token);
        if (!Matches(mime, prefix, length)) return BadRequest(new CodeResponse("invalid_image_upload"));
        stream.Position = 0;
        var key = routes.GetApiMartKey();
        if (key is null) return StatusCode(503, new CodeResponse("no_api_key"));
        var address = key.BaseUrl?.TrimEnd('/') ?? "";
        if (!Uri.TryCreate(address, UriKind.Absolute, out var baseUri) || baseUri.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(baseUri.UserInfo))
            return StatusCode(503, new CodeResponse("ai_unavailable"));
        if (address.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) address = address[..^3];
        using var request = new HttpRequestMessage(HttpMethod.Post, address + "/v1/uploads/images");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.ApiKey);
        using var multipart = new MultipartFormDataContent();
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(mime);
        // Use a generated filename; never forward local path or untrusted filename metadata.
        multipart.Add(content, "file", "reference." + (mime == "image/jpeg" ? "jpg" : mime[6..]));
        request.Content = multipart;
        using var client = http.CreateClient("AiProvider");
        try
        {
            using var response = await client.SendAsync(request, token);
            if (!response.IsSuccessStatusCode)
                return StatusCode(response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ? 429 : 502, new CodeResponse("image_upload_failed"));
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("url", out var value) || value.ValueKind != JsonValueKind.String ||
                !Uri.TryCreate(value.GetString(), UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(url.UserInfo))
                return StatusCode(502, new CodeResponse("image_upload_failed"));
            return Ok(new UploadResponse(url.AbsoluteUri));
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return StatusCode(504, new CodeResponse("image_upload_failed"));
        }
        catch (Exception error) when (error is HttpRequestException or JsonException)
        {
            return StatusCode(502, new CodeResponse("image_upload_failed"));
        }
    }

    private static bool Matches(string mime, byte[] bytes, int length) => mime switch
    {
        "image/png" => length >= 8 && bytes.AsSpan(0,8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}),
        "image/jpeg" => length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255,
        "image/gif" => length >= 6 && (System.Text.Encoding.ASCII.GetString(bytes,0,6) is "GIF87a" or "GIF89a"),
        "image/webp" => length >= 12 && System.Text.Encoding.ASCII.GetString(bytes,0,4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes,8,4) == "WEBP",
        _ => false
    };
}
