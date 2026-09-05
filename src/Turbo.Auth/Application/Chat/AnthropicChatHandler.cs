using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Turbo.Auth.Controllers.AI.Chat.Models;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Models.AI;
using Turbo.Auth.Models.AI.Chat;
using Message = Anthropic.SDK.Messaging.Message;

namespace Turbo.Auth.Application.Chat;

public class AnthropicChatHandler: IChatHandler
{
    public HandlerType ProviderType => HandlerType.Anthropic;

    /// <summary>
    /// 单次回复限制 (max_completion_tokens) 在「无限制」模式下的兜底值。
    /// Anthropic 的 <c>max_tokens</c> 为必填字段无法真正省略，故选用一个
    /// 能覆盖 Claude 3.5 / 3.7 / 4 系列默认输出上限的安全数字；模型自身
    /// 上限更小时由 Anthropic 服务端自动截断。
    /// </summary>
    private const int UnlimitedMaxTokens = 8192;

    public async Task Chat(NoModelChatBody chatBody, ModelKey modelKey, HttpResponse response,
        CancellationToken cancellationToken)
    {
        var client = new AnthropicClient(
            modelKey.SupplierKey!.ApiKey);
        // 「单次回复限制」语义：
        //   - chatBody.MaxCompletionTokens 为正数 → 用用户配置（前台「限制」模式）；
        //   - chatBody.MaxCompletionTokens 为 null 或 <=0 → 视作「无限制」，
        //     对应前台的 isMaxTokensUnlimited() 开关（HasValue = false = 字段缺失）
        //     与历史配置中曾以 0 表示无限制的兼容（resolveMaxCompletionTokens 已归一化）。
        var maxTokens = chatBody.MaxCompletionTokens is > 0
            ? chatBody.MaxCompletionTokens.Value
            : UnlimitedMaxTokens;
        var parameters = new MessageParameters()
        {
            Messages = TransferObject(chatBody.Messages!),
            MaxTokens = maxTokens,
            Model = modelKey.Model,
            Stream = true,
            Temperature = chatBody.Temperature.HasValue ? (decimal?)chatBody.Temperature.Value : null,
            TopP = chatBody.TopP.HasValue?(decimal?)chatBody.TopP.Value: null,

        };
        await foreach (var res in client.Messages.StreamClaudeMessageAsync(parameters))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (res.Delta != null)
            {
                if (res.Delta.Text != null)
                {
                    await response.WriteAsync(res.Delta.Text, cancellationToken);
                }
                
            }
        }
    }

    private static List<Message> TransferObject(IEnumerable<Turbo.Auth.Controllers.AI.Chat.Models.Message> messages)
    {
        var ms = new List<Message>();
        foreach (var message in messages)
        {
            switch (message.Role!.ToLower()!)
            {
                case OpenAiRole.SystemRole:
                    ms.Add(new Message()
                    {
                        Role = RoleType.User,
                        Content = message.Content
                    });
                    break;
                case OpenAiRole.UserRole:
                    ms.Add(new Message()
                    {
                        Role = RoleType.User,
                        Content = message.Content
                    });
                    break;
                case OpenAiRole.Assistant:
                    ms.Add(new Message()
                    {
                        Role = RoleType.Assistant,
                        Content = message.Content
                    });
                    break;
                default:
                    ms.Add(new Message()
                    {
                        Role = RoleType.User,
                        Content = message.Content
                    });
                    break;
            }
        }
        return ms;
    }
}
