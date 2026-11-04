using System.Text.Json;
using Turbo.Auth.Contracts.Chat;

namespace Turbo.Auth.Controllers.AI.Chat;

public static class ChatStreamWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static async Task Write(HttpResponse response, string type, CancellationToken token,
        string? text = null, string? code = null)
    {
        if (!response.HasStarted)
        {
            response.ContentType = "text/event-stream; charset=utf-8";
            response.Headers.CacheControl = "no-cache";
            response.Headers["X-Accel-Buffering"] = "no";
        }
        var payload = JsonSerializer.Serialize(new ChatStreamEventResponse(type, text, code), JsonOptions);
        await response.WriteAsync($"data: {payload}\n\n", token);
        await response.Body.FlushAsync(token);
    }
}
