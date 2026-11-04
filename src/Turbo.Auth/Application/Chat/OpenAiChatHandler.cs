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
/// 可选生成参数全部省略，由提供商使用模型默认值。
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

        // 把 ChatBody 内的消息转为 tryAGI 的 ChatCompletionRequestMessage 列表。
        var messages = TransferMessages(chatBody.Messages!, chatBody.Vision);

        // Only supply conversation data; leave optional generation settings unset.
        var variant = new CreateChatCompletionRequestVariant2
        {
            Messages = messages,
            Model = model,
            Stream = true,
        };

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

}
