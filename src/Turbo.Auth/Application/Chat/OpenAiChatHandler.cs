using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Betalgo.Ranul.OpenAI;
using Betalgo.Ranul.OpenAI.Contracts.Enums;
using Betalgo.Ranul.OpenAI.Managers;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Turbo.Auth.Controllers.AI.Chat.Models;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Models.AI.Chat;

namespace Turbo.Auth.Application.Chat;

public class OpenAiChatHandler : IChatHandler
{
    private readonly IHttpClientFactory _httpClientFactory;

    public OpenAiChatHandler(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public HandlerType ProviderType => HandlerType.Openai;

    public async Task Chat(NoModelChatBody chatBody, ModelKey modelKey, HttpResponse response,
        CancellationToken cancellationToken)
    {
        var url = modelKey.SupplierKey!.BaseUrl!.Trim();
        var uri = new Uri(url);
        var path = uri.AbsolutePath;
        var subRoute = path.TrimStart('/');
        var baseUrl = uri.GetLeftPart(UriPartial.Authority);

        var option = new OpenAIOptions()
        {
            ApiKey = modelKey.SupplierKey!.ApiKey!,
            BaseDomain = baseUrl,
        };
        if (baseUrl.Contains("azure.com"))
        {
            option.ProviderType = Betalgo.Ranul.OpenAI.ProviderType.Azure;
            option.DeploymentId = "jp-ai";
        }
        if (!string.IsNullOrEmpty(subRoute))
        {
            option.ApiVersion = subRoute;
        }

        var model = modelKey.Model!;
        var isReasoningModel = IsReasoningModel(model);
        var isGpt5Family = IsGpt5Family(model);

        // GPT-5 系列支持 verbosity，但 SDK 9.2.6 未暴露该字段。
        // 当用户传入 verbosity 且目标是 GPT-5 家族时走自定义 HTTP 通道。
        var wantsVerbosity = isGpt5Family && !string.IsNullOrWhiteSpace(chatBody.Verbosity);
        if (wantsVerbosity)
        {
            await StreamWithVerbosityAsync(option, chatBody, modelKey, response, cancellationToken);
            return;
        }

        var openAiService = new OpenAIService(option, _httpClientFactory.CreateClient("AiProvider"));
        var messages = TransferObject(chatBody.Messages!, chatBody.Vision);
        var completionResult = openAiService.ChatCompletion.CreateCompletionAsStream(new ChatCompletionCreateRequest
        {
            Messages = messages,
            Model = model,
            MaxCompletionTokens = chatBody.MaxCompletionTokens,
            Temperature = isReasoningModel ? null : FilterDouble(chatBody.Temperature),
            TopP = isReasoningModel ? null : FilterSpecial(chatBody.TopP, model),
            FrequencyPenalty = isReasoningModel ? null : FilterDouble(chatBody.FrequencyPenalty),
            PresencePenalty = isReasoningModel ? null : FilterSpecial(chatBody.PresencePenalty, model),
            ReasoningEffort = ResolveReasoningEffort(chatBody.ReasoningEffort, model, isReasoningModel)
        });
        await foreach (var completion in completionResult)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (completion.Successful)
            {
                if (completion.Choices.FirstOrDefault() == null) continue;
                if (completion.Choices.FirstOrDefault()?.Message == null) continue;
                if (completion.Choices.FirstOrDefault()?.Message.Content == null) continue;
                if (completion.Choices.FirstOrDefault()?.Message.Content!.Length > 0)
                {
                    await response.WriteAsync(completion.Choices.FirstOrDefault()?.Message.Content!, cancellationToken);
                }
            }
            else
            {
                if (completion.Error == null)
                {
                    throw new Exception("Unknown Error");
                }

                await response.WriteAsync($"{completion.Error.Code}: {completion.Error.Message}", cancellationToken);
            }
        }

        await response.CompleteAsync();
    }

    /// <summary>
    /// 当用户为 GPT-5 系列显式设置 verbosity 时，绕过 SDK 直接调用 /v1/chat/completions，
    /// 以便同时透传 verbosity、reasoning_effort 与其他采样参数。
    /// </summary>
    private static async Task StreamWithVerbosityAsync(
        OpenAIOptions option,
        NoModelChatBody chatBody,
        ModelKey modelKey,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var model = modelKey.Model!;
        var messages = TransferObject(chatBody.Messages!, chatBody.Vision);
        var isReasoningModel = IsReasoningModel(model);

        var payload = new JObject
        {
            ["model"] = model,
            ["messages"] = JArray.FromObject(messages, JsonSerializer.CreateDefault()),
            ["stream"] = true,
            ["verbosity"] = NormalizeVerbosity(chatBody.Verbosity!) ?? "medium"
        };
        if (chatBody.MaxCompletionTokens.HasValue)
        {
            payload["max_completion_tokens"] = chatBody.MaxCompletionTokens.Value;
        }
        payload["reasoning_effort"] = ResolveReasoningEffortString(chatBody.ReasoningEffort, model, isReasoningModel);
        // 推理模型/GPT-5 系列不接受传统采样参数；不再附加 temperature/top_p/penalties。

        var baseUrl = (option.BaseDomain ?? "https://api.openai.com").TrimEnd('/');
        var apiVersion = option.ProviderType == Betalgo.Ranul.OpenAI.ProviderType.Azure
            ? (option.ApiVersion ?? "v1")
            : "v1";
        var endpoint = $"{baseUrl}/{apiVersion}/chat/completions";

        using var http = new HttpRequestMessage(HttpMethod.Post, endpoint);
        http.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
        if (!string.IsNullOrEmpty(option.ApiKey))
        {
            http.Headers.Authorization = new AuthenticationHeaderValue("Bearer", option.ApiKey);
        }

        var client = new HttpClient();
        try
        {
            using var upstream = await client.SendAsync(http, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!upstream.IsSuccessStatusCode)
            {
                var err = await upstream.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"OpenAI upstream error {(int)upstream.StatusCode}: {err}");
            }

            await using var stream = await upstream.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var sb = new StringBuilder();
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(line)) continue;
                if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
                var data = line["data:".Length..].Trim();
                if (data.Length == 0 || data == "[DONE]") continue;
                JToken? token;
                try
                {
                    token = JToken.Parse(data);
                }
                catch (JsonException)
                {
                    continue;
                }

                var content = (string?)token["choices"]?[0]?["delta"]?["content"];
                if (!string.IsNullOrEmpty(content))
                {
                    sb.Clear();
                    sb.Append(content);
                    await response.WriteAsync(sb.ToString(), cancellationToken);
                }
            }
        }
        finally
        {
            client.Dispose();
        }

        await response.CompleteAsync();
    }

    private static bool IsReasoningModel(string model)
    {
        if (string.IsNullOrEmpty(model)) return false;
        var lower = model.ToLowerInvariant();
        if (lower.StartsWith("o1") || lower.StartsWith("o3") || lower.StartsWith("o4"))
        {
            return true;
        }
        if (IsGpt5Family(model))
        {
            return true;
        }
        return false;
    }

    private static bool IsGpt5Family(string model)
    {
        if (string.IsNullOrEmpty(model)) return false;
        var lower = model.ToLowerInvariant();
        return lower.StartsWith("gpt-5");
    }

    /// <summary>
    /// SDK 9.2.6 的 ReasoningEffort 枚举仅支持 Low / Medium / High，
    /// 这里把请求中可能传入的 "none"/"minimal"/"xhigh" 映射到 SDK 支持的值。
    /// </summary>
    private static ReasoningEffort? ResolveReasoningEffort(string? requested, string model, bool isReasoningModel)
    {
        if (!isReasoningModel || string.IsNullOrWhiteSpace(requested))
        {
            return null;
        }

        var value = requested.Trim().ToLowerInvariant();
        return value switch
        {
            "low" => ReasoningEffort.Low,
            "medium" => ReasoningEffort.Medium,
            "high" => ReasoningEffort.High,
            // none/minimal/xhigh 在 SDK 枚举中不存在
            "none" or "minimal" or "xhigh" => ReasoningEffort.Medium,
            _ => null
        };
    }

    /// <summary>
    /// 与 <see cref="ResolveReasoningEffort"/> 相同的映射，但返回字符串形式，
    /// 用于自定义 JSON 体（透传原始枚举值）。
    /// </summary>
    private static string ResolveReasoningEffortString(string? requested, string model, bool isReasoningModel)
    {
        if (!isReasoningModel || string.IsNullOrWhiteSpace(requested))
        {
            return "medium";
        }

        var value = requested.Trim().ToLowerInvariant();
        return value switch
        {
            "none" or "minimal" or "low" or "medium" or "high" or "xhigh" => value,
            _ => "medium"
        };
    }

    private static string? NormalizeVerbosity(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var v = raw.Trim().ToLowerInvariant();
        return v is "low" or "medium" or "high" ? v : null;
    }

    private static float? FilterSpecial(double? p, string model)
    {
        if (model.StartsWith("o", StringComparison.CurrentCultureIgnoreCase) || model.Contains("mi"))
        {
            return null;
        }
        return ToFloat(p);
    }

    private static float? FilterDouble(double? p) => ToFloat(p);

    private static float? ToFloat(double? p)
    {
        if (!p.HasValue) return null;
        if (double.IsNaN(p.Value) || double.IsInfinity(p.Value)) return null;
        return Convert.ToSingle(p.Value, CultureInfo.InvariantCulture);
    }

    private static List<ChatMessage> TransferObject(IEnumerable<Message> messages, bool vision = false)
    {

        var ms = new List<ChatMessage>();
        foreach (var message in messages)
        {
            switch (message.Role!.ToLower()!)
            {
                case OpenAiRole.SystemRole:
                    ms.Add(ChatMessage.FromSystem(message.Content! as string));
                    break;
                case OpenAiRole.UserRole:
                    if (vision)
                    {
                        var mcl = new List<MessageContent>();
                        foreach (var vc in JsonConvert.DeserializeObject<VisionMessage>(JsonConvert.SerializeObject(message))!.Content)
                        {
                            if (vc.Type == "text")
                            {
                                mcl.Add(new MessageContent()
                                {
                                    Type = vc.Type!,
                                    Text = vc.Text,
                                });
                            }
                            else
                            {
                                mcl.Add(new MessageContent()
                                {
                                    Type = vc.Type!,
                                    ImageUrl = new MessageImageUrl()
                                    {
                                        Url = vc.VisionImage!.Url!,
                                        Detail = vc.VisionImage.Detail!
                                    }
                                });
                            }


                        }
                        ms.Add(ChatMessage.FromUser(mcl));
                    }
                    else
                    {
                        ms.Add(ChatMessage.FromUser(message.Content! as string));
                    }

                    break;
                case OpenAiRole.Assistant:
                    ms.Add(ChatMessage.FromAssistant(message.Content! as string));
                    break;
                default:
                    ms.Add(ChatMessage.FromUser(message.Content! as string));
                    break;
            }
        }
        return ms;
    }
}

public class OpenAiRole
{
    public const string SystemRole = "system";
    public const string Assistant = "assistant";
    public const string UserRole = "user";
}