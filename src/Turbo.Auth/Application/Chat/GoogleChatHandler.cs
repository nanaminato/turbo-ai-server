using System.Diagnostics;
using System.Globalization;
using System.Text;
using DotnetGeminiSDK.Client;
using DotnetGeminiSDK.Config;
using DotnetGeminiSDK.Model;
using DotnetGeminiSDK.Model.Request;
using DotnetGeminiSDK.Requester;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Turbo.Auth.Controllers.AI.Chat.Models;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Models.AI.Chat;
using Part = DotnetGeminiSDK.Model.Request.Part;

namespace Turbo.Auth.Application.Chat;

public class GoogleChatHandler : IChatHandler
{
    private const string DefaultBaseUrl = "https://generativelanguage.googleapis.com";
    private const string ApiVersionSegment = "v1beta";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiRequester> _apiRequesterLogger;

    public GoogleChatHandler(IHttpClientFactory httpClientFactory, ILogger<ApiRequester> apiRequesterLogger)
    {
        _httpClientFactory = httpClientFactory;
        _apiRequesterLogger = apiRequesterLogger;
    }

    public HandlerType ProviderType => HandlerType.Google;

    public async Task Chat(NoModelChatBody chatBody, ModelKey modelKey, HttpResponse response,
        CancellationToken cancellationToken)
    {
        var model = modelKey.Model!;

        // 对于需要 Gemini 2.5 thinking_budget 或 system_instruction 等
        // SDK 尚未支持的字段，直接构造 JSON 请求体走 HTTP 通道。
        var useRawHttp = NeedsRawHttp(chatBody, model);

        if (useRawHttp)
        {
            await StreamWithRawHttpAsync(chatBody, modelKey, response, cancellationToken);
            return;
        }

        // 经典路径：完全使用 SDK（适用于不需要新参数的旧 Gemini 1.x 等模型）
        await StreamWithSdkAsync(chatBody, modelKey, response, cancellationToken);
    }

    private static bool NeedsRawHttp(NoModelChatBody chatBody, string model)
    {
        // Gemini 2.5 系列才支持思考预算；只要用户提供了该参数就走原生 HTTP。
        if (chatBody.ThinkingBudget.HasValue) return true;
        // 显式传入 verbosity/reasoning_effort 但模型为 Gemini 时，识别为占位用（Gemini 不接受这些字段）。
        // 这里仅作决策：thinking_budget 唯一触发。
        var lower = model.ToLowerInvariant();
        return lower.StartsWith("gemini-2.5");
    }

    private async Task StreamWithSdkAsync(NoModelChatBody chatBody, ModelKey modelKey, HttpResponse response,
        CancellationToken cancellationToken)
    {
        var messages = TransferObject(chatBody.Messages, chatBody.Vision);
        var geminiClient = new GeminiClient(
            new GoogleGeminiConfig()
            {
                ApiKey = modelKey.SupplierKey!.ApiKey!
            },
            new ApiRequester(_httpClientFactory, _apiRequesterLogger)
        );
        await geminiClient.StreamTextPrompt(messages, async (chunck) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            chunck = FillBlock(chunck);
            var geminiParts = JsonConvert.DeserializeObject<GeminiPart[]>(chunck);
            if (geminiParts == null) return;
            foreach (var block in geminiParts)
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (block != null)
                {
                    await response.WriteAsync(block!.Candidates![0]!.Content.Parts[0].Text, cancellationToken);
                }
            }
        });
    }

    private async Task StreamWithRawHttpAsync(NoModelChatBody chatBody, ModelKey modelKey, HttpResponse response,
        CancellationToken cancellationToken)
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

        var generationConfig = BuildGenerationConfig(chatBody, model);
        if (generationConfig != null)
        {
            payload["generationConfig"] = generationConfig;
        }

        var http = _httpClientFactory.CreateClient("AiProvider");
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
            catch (JsonException)
            {
                continue;
            }
            var parts = token["candidates"]?[0]?["content"]?["parts"];
            if (parts is not JArray arr) continue;
            foreach (var part in arr)
            {
                var text = (string?)part["text"];
                if (!string.IsNullOrEmpty(text))
                {
                    await response.WriteAsync(text!, cancellationToken);
                }
            }
        }
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
    private static (JObject? systemInstruction, JArray contents) BuildContents(NoModelChatBody chatBody)
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

    private static JObject BuildGenerationConfig(NoModelChatBody chatBody, string model)
    {
        var cfg = new JObject();
        var hasAny = false;

        if (chatBody.ThinkingBudget.HasValue)
        {
            cfg["thinkingConfig"] = new JObject
            {
                ["thinkingBudget"] = chatBody.ThinkingBudget.Value
            };
            hasAny = true;
        }

        if (chatBody.Temperature.HasValue)
        {
            cfg["temperature"] = ToFiniteNumber(chatBody.Temperature.Value);
            hasAny = true;
        }
        if (chatBody.TopP.HasValue)
        {
            cfg["topP"] = ToFiniteNumber(chatBody.TopP.Value);
            hasAny = true;
        }
        // 单次回复限制 (max_completion_tokens)：
        //   - HasValue 且为正数 → 透传给 Gemini 的 maxOutputTokens（前台「限制」模式）；
        //   - HasValue=false 或 <=0 → 视作「无限制」，跳过该字段，
        //     覆盖前台 isMaxTokensUnlimited() 开关以及历史以 0 表示无限制的配置。
        if (chatBody.MaxCompletionTokens is > 0)
        {
            cfg["maxOutputTokens"] = chatBody.MaxCompletionTokens.Value;
            hasAny = true;
        }

        return hasAny ? cfg : null!;
    }

    private static double ToFiniteNumber(double value) =>
        double.IsNaN(value) || double.IsInfinity(value) ? 0 : value;

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

    private static List<Content> TransferObject(Message[]? chatBodyMessages, bool vision = false)
    {
        var parts = new List<Content>();

        if (!vision)
        {
            foreach (var message in chatBodyMessages!)
            {
                switch (message.Role!.ToLower())
                {
                    case OpenAiRole.SystemRole:
                        parts.Add(new()
                        {
                            Role = GoogleRoles.User,
                            Parts = new List<Part>()
                            {
                                new()
                                {
                                    Text = message!.Content! as string,
                                }
                            }
                        });
                        break;
                    case OpenAiRole.UserRole:
                        parts.Add(new()
                        {
                            Role = GoogleRoles.User,
                            Parts = new List<Part>()
                            {
                                new()
                                {
                                    Text = message!.Content! as string
                                }
                            }
                        });

                        break;
                    case OpenAiRole.Assistant:
                        parts.Add(new()
                        {
                            Role = GoogleRoles.Model,
                            Parts = new List<Part>()
                            {
                                new()
                                {
                                    Text = message!.Content! as string
                                }
                            }
                        });
                        break;
                    default:
                        parts.Add(new()
                        {
                            Role = GoogleRoles.User,
                            Parts = new List<Part>()
                            {
                                new()
                                {
                                    Text = message!.Content!,
                                }
                            }
                        });
                        break;
                }
            }
        }
        else
        {
            var message = chatBodyMessages.Last();
            var content = new Content();
            content.Role = GoogleRoles.User;
            parts.Add(content);
            var contentParts = new List<Part>();
            content.Parts = contentParts;
            foreach (var vc in JsonConvert.DeserializeObject<VisionMessage>(JsonConvert.SerializeObject(message))!
                         .Content)
            {
                if (vc.Type == "text")
                {
                    contentParts.Add(new Part()
                    {
                        Text = vc.Text
                    });
                }
                else
                {
                    var inlineData = GetInlineData(vc.VisionImage.Url);
                    contentParts.Add(new Part()
                    {
                        InlineData = inlineData
                    });
                }
            }
        }


        return parts;
    }

    private static InlineData? GetInlineData(string url)
    {
        var parts = url.Split(',');
        var mimeType = parts[0].Split(':')[1].Split(';')[0];
        var data = parts[1];
        return new InlineData()
        {
            Data = data,
            MimeType = mimeType
        };
    }

    private string FillBlock(string chunck)
    {
        var light = chunck.Trim();
        if (!light.StartsWith('['))
        {
            light = '[' + light;
        }

        if (!light.EndsWith(']'))
        {
            light += ']';
        }

        return light;
    }
}

public class GoogleRoles
{
    public const string User = "user";
    public const string Model = "model";
}