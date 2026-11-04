using Turbo.Auth.Application.Routing;
using tryAGI.OpenAI;

namespace Turbo.Auth.Application.Providers;

public sealed class OpenAiClientFactory(IHttpClientFactory clients)
{
    public OpenAiClient Create(ModelKey route, TimeSpan? timeout = null)
    {
        var http = clients.CreateClient("AiProvider");
        if (timeout.HasValue) http.Timeout = timeout.Value;
        return new OpenAiClient(route.SupplierKey!.ApiKey!, httpClient: http,
            baseUri: new Uri(ResolveBaseUrl(route.SupplierKey.BaseUrl)), disposeHttpClient: true);
    }

    private static string ResolveBaseUrl(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return "https://api.openai.com/v1";
        var trimmed = configured.Trim().TrimEnd('/');
        if (!trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) && !trimmed.Contains("/v", StringComparison.Ordinal))
            trimmed += "/v1";
        return trimmed;
    }
}
