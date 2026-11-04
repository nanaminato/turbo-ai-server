using System.Text.Json;

namespace Turbo.Auth.Controllers.AI.Chat;

public static class ChatStreamWriter
{
    public static async Task Write(HttpResponse response, string type, CancellationToken token,
        string? text = null, string? code = null)
    {
        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";
        var payload = JsonSerializer.Serialize(new { type, text, code });
        await response.WriteAsync($"data: {payload}\n\n", token);
        await response.Body.FlushAsync(token);
    }
}
