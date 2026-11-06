using RelaxKon.Auth.Application.Providers;
using RelaxKon.Auth.Application.Routing;

namespace RelaxKon.Auth.Application.Chat;

// Model Studio's officially documented OpenAI-compatible chat protocol shares the OpenAI SDK adapter.
public sealed class AlibabaChatHandler(OpenAiClientFactory clients, ILogger<OpenAiChatHandler>? logger = null) : IChatHandler
{
    public HandlerType ProviderType => HandlerType.Alibaba;
    public IAsyncEnumerable<string> Stream(ChatRequest request, ModelKey route, CancellationToken token) =>
        new OpenAiChatHandler(clients, logger).StreamAt(request, route, Endpoint(route.SupplierKey?.BaseUrl), token);
    internal static string Endpoint(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var uri = new Uri(configured.Trim().TrimEnd('/'));
        // Preserve the key's region/workspace. Existing native DashScope roots map to compatible mode.
        if (uri.Host.EndsWith(".aliyuncs.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.TrimEnd('/') is "" or "/" or "/api/v1")
            return uri.GetLeftPart(UriPartial.Authority) + "/compatible-mode/v1";
        return configured;
    }
}
