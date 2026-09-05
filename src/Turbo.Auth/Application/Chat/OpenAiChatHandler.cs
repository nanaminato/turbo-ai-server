using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Controllers.AI.Chat.Models;
using Turbo.Auth.Models.AI.Chat;
using tryAGI.OpenAI;
using Message = Turbo.Auth.Controllers.AI.Chat.Models.Message;
using VisionMessage = Turbo.Auth.Controllers.AI.Chat.Models.VisionMessage;

namespace Turbo.Auth.Application.Chat;

/// <summary>
/// OpenAI / OpenAI 兼容厂商的 Chat 路由处理器（基于 tryAGI.OpenAI 4.2.10）。
/// 直接通过 SDK 调用 Chat Completion 流式端点，
/// 自动支持 GPT-5 / GPT-5.1 / GPT-5.2 / GPT-5.3+ 系列的 verbosity 与 reasoning_effort 字段。
/// </summary>
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
        var baseUrl = ResolveBaseUrl(modelKey.SupplierKey?.BaseUrl);
        var apiKey = modelKey.SupplierKey!.ApiKey!;

        // 共享的 HttpClient 由 IHttpClientFactory 管理；tryAGI 默认 Bearer 鉴权，
        // 由构造器根据 apiKey 设置 Authorization 头，不需要手动添加 EndPointAuthorization。
        var http = _httpClientFactory.CreateClient("AiProvider");
        var client = new OpenAiClient(apiKey, baseUri: new Uri(baseUrl));

        var model = modelKey.Model!;
        var isReasoningModel = IsReasoningModel(model);

        // 把 ChatBody 内的消息转为 tryAGI 的 ChatCompletionRequestMessage 列表。
        var messages = TransferMessages(chatBody.Messages!, chatBody.Vision);

        // Build variant 2。tryAGI 4.2.10 的 CreateChatCompletionRequestVariant2 包含：
        //   Messages, Model, Stream, Verbosity, ReasoningEffort, MaxCompletionTokens,
        //   FrequencyPenalty, PresencePenalty 等。
        // 注意：Variant2 上没有 Temperature / TopP 属性（4.2.10 OpenAPI spec 删除了）；
        // 对非推理模型，通过 AdditionalProperties 扩展字段塞进去（标记了 [JsonExtensionData]）。
        var variant = new CreateChatCompletionRequestVariant2
        {
            Messages = messages,
            Model = model,
            Stream = true,
        };

        // 单次回复限制 (max_completion_tokens)：
        //   - HasValue 且为正数 → 透传给 OpenAI（前台「限制」模式）；
        //   - HasValue=false 或 <=0 → 视作「无限制」，跳过该字段，
        //     覆盖前台 isMaxTokensUnlimited() 开关以及历史以 0 表示无限制的配置。
        if (chatBody.MaxCompletionTokens is > 0)
        {
            variant.MaxCompletionTokens = chatBody.MaxCompletionTokens;
        }

        // GPT-5 / GPT-5.x 支持 verbosity。VerbosityEnum 仅 Low/Medium/High。
        var verbosity = ParseVerbosity(chatBody.Verbosity);
        if (verbosity.HasValue)
        {
            variant.Verbosity = verbosity.Value;
        }

        // 仅推理模型（o 系列 + GPT-5 系列）允许 reasoning_effort。
        var reasoning = ParseReasoningEffort(chatBody.ReasoningEffort, isReasoningModel);
        if (reasoning.HasValue)
        {
            variant.ReasoningEffort = reasoning.Value;
        }

        // 非推理模型可附加传统采样参数；推理模型显式不附加（OpenAI 官方限制）。
        // 注意：Variant2 上没有 Temperature / TopP 属性，通过 AdditionalProperties 兜底。
        if (!isReasoningModel)
        {
            if (chatBody.Temperature.HasValue && IsFinite(chatBody.Temperature.Value))
            {
                variant.AdditionalProperties["temperature"] =
                    Convert.ToSingle(chatBody.Temperature.Value, CultureInfo.InvariantCulture);
            }
            if (chatBody.TopP.HasValue && IsFinite(chatBody.TopP.Value))
            {
                variant.AdditionalProperties["top_p"] =
                    Convert.ToSingle(chatBody.TopP.Value, CultureInfo.InvariantCulture);
            }
            if (chatBody.FrequencyPenalty.HasValue && IsFinite(chatBody.FrequencyPenalty.Value))
            {
                variant.FrequencyPenalty = Convert.ToSingle(chatBody.FrequencyPenalty.Value, CultureInfo.InvariantCulture);
            }
            if (chatBody.PresencePenalty.HasValue && IsFinite(chatBody.PresencePenalty.Value))
            {
                variant.PresencePenalty = Convert.ToSingle(chatBody.PresencePenalty.Value, CultureInfo.InvariantCulture);
            }
        }

        var request = new CreateChatCompletionRequest
        {
            CreateChatCompletionRequestVariant2 = variant,
        };

        // CreateChatCompletionAsStreamAsync 在 tryAGI 4.2.10 中按 string 输出（基于内部隐式转换）。
        await foreach (var chunk in client.Chat.CreateChatCompletionAsStreamAsync(request, cancellationToken: cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(chunk)) continue;
            await response.WriteAsync(chunk, cancellationToken);
        }

        await response.CompleteAsync();
    }

    /// <summary>
    /// 把 <see cref="NoModelChatBody"/> 内的 <see cref="Message"/> 列表转换为 tryAGI 的
    /// <see cref="ChatCompletionRequestMessage"/> 列表。支持文本 / Vision (text + image_url) 复合消息。
    /// </summary>
    private static List<ChatCompletionRequestMessage> TransferMessages(
        IEnumerable<Message> messages, bool vision = false)
    {
        var result = new List<ChatCompletionRequestMessage>();
        foreach (var message in messages)
        {
            var role = (message.Role ?? string.Empty).ToLowerInvariant();
            switch (role)
            {
                case OpenAiRole.SystemRole:
                    result.Add(ConvertSystemMessage(message));
                    break;
                case OpenAiRole.UserRole:
                    if (vision)
                    {
                        result.Add(ConvertVisionUserMessage(message));
                    }
                    else
                    {
                        var text = ExtractText(message.Content);
                        result.Add(text.AsUserMessage());
                    }
                    break;
                case OpenAiRole.Assistant:
                    var assistantText = ExtractText(message.Content);
                    result.Add(assistantText.AsAssistantMessage());
                    break;
                default:
                    var fallback = ExtractText(message.Content);
                    result.Add(fallback.AsUserMessage());
                    break;
            }
        }
        return result;
    }

    private static ChatCompletionRequestMessage ConvertSystemMessage(Message message)
    {
        // System 消息只支持文本内容；如果是结构化的，直接取 Content 字符串部分。
        var text = ExtractText(message.Content);
        return text.AsSystemMessage();
    }

    private static ChatCompletionRequestMessage ConvertVisionUserMessage(Message message)
    {
        // 把当前 message 序列化为 VisionMessage，然后构造 ChatCompletionRequestUserMessageContentPart 列表。
        var vm = JsonConvert.DeserializeObject<VisionMessage>(JsonConvert.SerializeObject(message));
        var parts = new List<ChatCompletionRequestUserMessageContentPart>();
        if (vm?.Content != null)
        {
            foreach (var vc in vm.Content)
            {
                if (string.Equals(vc.Type, "text", StringComparison.OrdinalIgnoreCase))
                {
                    // 显式构造文本 content part（FromString 在 SDK 里是写死 "describe the following image"，
                    // 不能用作通用文本输入；因此走构造函数路径）。
                    parts.Add(new ChatCompletionRequestUserMessageContentPart(
                        new ChatCompletionRequestMessageContentPartText
                        {
                            Text = vc.Text ?? string.Empty,
                            Type = ChatCompletionRequestMessageContentPartTextType.Text,
                        }));
                }
                else
                {
                    var url = vc.VisionImage?.Url ?? string.Empty;
                    // 直接使用图片 URL；OpenAI 也支持 base64 data url 形式。
                    parts.Add(new ChatCompletionRequestUserMessageContentPart(
                        new ChatCompletionRequestMessageContentPartImage
                        {
                            ImageUrl = new ChatCompletionRequestMessageContentPartImageImageUrl
                            {
                                Url = url,
                                Detail = ChatCompletionRequestMessageContentPartImageImageUrlDetail.Auto,
                            },
                            Type = ChatCompletionRequestMessageContentPartImageType.ImageUrl,
                        }));
                }
            }
        }
        // 构造 user message；Content 是 OneOf<string, IList<ContentPart>>，
        // 不能直接赋 IList，必须用 OneOf<T1,T2> 显式包一层。
        return new ChatCompletionRequestUserMessage(
            new global::tryAGI.OpenAI.OneOf<string, IList<ChatCompletionRequestUserMessageContentPart>>(parts),
            ChatCompletionRequestUserMessageRole.User,
            name: null);
    }

    private static string ExtractText(object? content)
    {
        if (content == null) return string.Empty;
        if (content is string s) return s;
        // 结构化内容走 VisionMessage 反序列化兜底取 text。
        try
        {
            var vm = JsonConvert.DeserializeObject<VisionMessage>(JsonConvert.SerializeObject(content));
            if (vm?.Content != null)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var c in vm.Content)
                {
                    if (string.Equals(c.Type, "text", StringComparison.OrdinalIgnoreCase) && c.Text != null)
                    {
                        sb.Append(c.Text);
                    }
                }
                return sb.ToString();
            }
        }
        catch
        {
            // ignore
        }
        return content.ToString() ?? string.Empty;
    }

    private static string ResolveBaseUrl(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            // OpenAiClient.DefaultBaseUrl = "https://api.openai.com/v1"
            return "https://api.openai.com/v1";
        }
        var trimmed = configured.Trim().TrimEnd('/');
        // 兼容配置只写 host 的情况：自动补 /v1
        if (!trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Contains("/v", StringComparison.Ordinal))
        {
            trimmed += "/v1";
        }
        return trimmed;
    }

    private static bool IsReasoningModel(string model)
    {
        if (string.IsNullOrEmpty(model)) return false;
        var lower = model.ToLowerInvariant();
        if (lower.StartsWith("o1") || lower.StartsWith("o3") || lower.StartsWith("o4"))
        {
            return true;
        }
        if (lower.StartsWith("gpt-5"))
        {
            return true;
        }
        return false;
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    /// <summary>
    /// 把字符串映射到 tryAGI 的 <see cref="VerbosityEnum"/>。VerbosityEnum 仅含 Low/Medium/High。
    /// 未知值返回 null（不发送 verbosity 字段）。
    /// </summary>
    private static VerbosityEnum? ParseVerbosity(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Trim().ToLowerInvariant() switch
        {
            "low" => VerbosityEnum.Low,
            "medium" => VerbosityEnum.Medium,
            "high" => VerbosityEnum.High,
            // Minimal/Xhigh 在 VerbosityEnum 中不存在，忽略。
            _ => null,
        };
    }

    /// <summary>
    /// 把字符串映射到 tryAGI 的 <see cref="ReasoningEffortEnum"/>。
    /// ReasoningEffortEnum 含 None/Minimal/Low/Medium/High/Max/Xhigh。
    /// </summary>
    private static ReasoningEffortEnum? ParseReasoningEffort(string? raw, bool isReasoningModel)
    {
        if (!isReasoningModel || string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Trim().ToLowerInvariant() switch
        {
            "none" => ReasoningEffortEnum.None,
            "minimal" => ReasoningEffortEnum.Minimal,
            "low" => ReasoningEffortEnum.Low,
            "medium" => ReasoningEffortEnum.Medium,
            "high" => ReasoningEffortEnum.High,
            "xhigh" => ReasoningEffortEnum.Xhigh,
            "max" => ReasoningEffortEnum.Max,
            _ => null,
        };
    }
}