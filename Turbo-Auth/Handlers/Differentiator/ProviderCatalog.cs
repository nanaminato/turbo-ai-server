namespace Turbo_Auth.Handlers.Differentiator;

public sealed record ProviderDefinition(HandlerType Type, string DisplayName, bool SupportsChat);

public static class ProviderCatalog
{
    private static readonly ProviderDefinition[] All =
    [
        new(HandlerType.Openai, "OpenAI", true),
        new(HandlerType.Google, "Google", true),
        new(HandlerType.Anthropic, "Anthropic", true),
        new(HandlerType.Novita, "Novita", false),
        new(HandlerType.Alibaba, "Alibaba", true),
        new(HandlerType.Twitter, "Twitter", false),
        new(HandlerType.ApiMart, "apiMart", false)
    ];

    public static IReadOnlyList<ProviderDefinition> GetAll() => All;

    public static bool SupportsChat(int requestIdentifier) =>
        All.Any(provider => (int)provider.Type == requestIdentifier && provider.SupportsChat);
}
