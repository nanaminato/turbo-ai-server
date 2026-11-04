using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Models.AI.Chat;

namespace Turbo.Auth.Application.Chat;

public class GoogleChatHandler : IChatHandler
{
    private const string DefaultBaseUrl = "https://generativelanguage.googleapis.com";
    private const string ApiVersionSegment = "v1beta";

    private readonly IHttpClientFactory _httpClientFactory;
    public GoogleChatHandler(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public HandlerType ProviderType => HandlerType.Google;

    public async IAsyncEnumerable<string> Stream(ChatRequest chatBody, ModelKey modelKey,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var apiKey = modelKey.SupplierKey!.ApiKey!;
        var model = modelKey.Model!;

        var baseUrl = ResolveBaseUrl(modelKey.SupplierKey!.BaseUrl);
        var url = $"{baseUrl.TrimEnd('/')}/{ApiVersionSegment}/models/{Uri.EscapeDataString(model)}:streamGenerateContent?alt=sse";

        // 构造 systemInstruction 与 contents
        var (systemInstruction, contents) = BuildContents(chatBody);

        var payload = new JObject
        {
            ["contents"] = contents
        };
        if (systemInstruction != null)
        {
            payload["systemInstruction"] = systemInstruction;
        }

        using var http = _httpClientFactory.CreateClient("AiProvider");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        // Gemini 流式接口需要在 Header 中携带 x-goog-api-key 而非查询串；
        // 为了与 SDK 默认行为兼容（query key），我们同时支持两种。
        var useHeaderKey = baseUrl.Contains("googleapis.com", StringComparison.OrdinalIgnoreCase) ||
                           baseUrl.Contains("vertexai", StringComparison.OrdinalIgnoreCase);
        if (useHeaderKey)
        {
            request.Headers.Add("x-goog-api-key", apiKey);
        }
        else
        {
            // 第三方代理通常使用查询串 ?key=
            var separator = url.Contains('?') ? '&' : '?';
            request.RequestUri = new Uri($"{url}{separator}key={Uri.EscapeDataString(apiKey)}");
        }
        request.Content = new StringContent(payload.ToString(Newtonsoft.Json.Formatting.None),
            Encoding.UTF8, "application/json");

        using var upstream = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!upstream.IsSuccessStatusCode)
        {
            var err = await upstream.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Gemini upstream error {(int)upstream.StatusCode}: {err}");
        }

        await using var stream = await upstream.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var finished = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break; // EOF
            if (string.IsNullOrEmpty(line)) continue;
            // SSE 格式：行首为 "data: "，其余为 JSON
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var data = line["data:".Length..].Trim();
            if (data.Length == 0) continue;
            JToken token;
            try
            {
                token = JToken.Parse(data);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Invalid Gemini stream event.", exception);
            }
            if (token["error"] is not null) throw new InvalidOperationException("Gemini stream failed.");
            finished |= !string.IsNullOrEmpty((string?)token["candidates"]?[0]?["finishReason"]);
            var parts = token["candidates"]?[0]?["content"]?["parts"];
            if (parts is not JArray arr) continue;
            foreach (var part in arr)
            {
                var text = (string?)part["text"];
                if (!string.IsNullOrEmpty(text))
                {
                    yield return text!;
                }
            }
        }
        if (!finished) throw new EndOfStreamException("Gemini stream ended without a finish reason.");
    }

    private static string ResolveBaseUrl(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return DefaultBaseUrl;
        var trimmed = configured.Trim();
        // 支持第三方代理：保留原 BaseUrl
        return trimmed;
    }

    /// <summary>
    /// 构造 systemInstruction（来自首条 system 消息）与 contents（其余消息）。
    /// </summary>
    private static (JObject? systemInstruction, JArray contents) BuildContents(ChatRequest chatBody)
    {
        JObject? systemInstruction = null;
        var contents = new JArray();

        if (chatBody.Messages is null) return (systemInstruction, contents);

        var messages = chatBody.Messages;
        var firstIndex = 0;
        if (messages.Length > 0 &&
            string.Equals(messages[0].Role, OpenAiRole.SystemRole, StringComparison.OrdinalIgnoreCase))
        {
            systemInstruction = new JObject
            {
                ["parts"] = new JArray
                {
                    new JObject { ["text"] = messages[0].Content as string ?? string.Empty }
                }
            };
            firstIndex = 1;
        }

        // 如果开启了视觉模式，将最后一条用户消息的 content 视为 VisionMessage JSON。
        for (var i = firstIndex; i < messages.Length; i++)
        {
            var msg = messages[i];
            var role = NormalizeGeminiRole(msg.Role);
            var parts = new JArray();

            if (chatBody.Vision && i == messages.Length - 1 &&
                string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
            {
                var vision = JsonConvert.DeserializeObject<VisionMessage>(JsonConvert.SerializeObject(msg));
                if (vision?.Content != null)
                {
                    foreach (var vc in vision.Content)
                    {
                        if (string.Equals(vc.Type, "text", StringComparison.OrdinalIgnoreCase))
                        {
                            parts.Add(new JObject { ["text"] = vc.Text ?? string.Empty });
                        }
                        else if (vc.VisionImage != null)
                        {
                            var inline = TryParseDataUrl(vc.VisionImage.Url);
                            if (inline != null)
                            {
                                parts.Add(inline);
                            }
                        }
                    }
                }
            }
            else
            {
                parts.Add(new JObject { ["text"] = msg.Content as string ?? string.Empty });
            }

            contents.Add(new JObject
            {
                ["role"] = role,
                ["parts"] = parts
            });
        }

        return (systemInstruction, contents);
    }

    private static string NormalizeGeminiRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return "user";
        return role.ToLowerInvariant() switch
        {
            OpenAiRole.SystemRole => "user", // system 已经被提升到 systemInstruction
            OpenAiRole.Assistant => GoogleRoles.Model,
            OpenAiRole.UserRole => GoogleRoles.User,
            _ => GoogleRoles.User
        };
    }

    private static JObject? TryParseDataUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return null;
        var commaIdx = url.IndexOf(',');
        if (commaIdx < 0) return null;
        var header = url[..commaIdx];
        var data = url[(commaIdx + 1)..];
        var mimeType = "application/octet-stream";
        var parts = header.Split(';');
        if (parts.Length > 0 && parts[0].StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            mimeType = parts[0]["data:".Length..];
        }
        return new JObject
        {
            ["inline_data"] = new JObject
            {
                ["mime_type"] = mimeType,
                ["data"] = data
            }
        };
    }

 }

public class GoogleRoles
{
    public const string User = "user";
    public const string Model = "model";
}
