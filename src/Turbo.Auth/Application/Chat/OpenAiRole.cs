namespace Turbo.Auth.Application.Chat;

/// <summary>
/// OpenAI 兼容模型的角色常量。其它供应商（Google / Anthropic）的 handler 也复用此值，
/// 因为 ChatBody 的 Role 字段在内部统一存储为 OpenAI 风格的字符串。
/// </summary>
public class OpenAiRole
{
    public const string SystemRole = "system";
    public const string Assistant = "assistant";
    public const string UserRole = "user";
}