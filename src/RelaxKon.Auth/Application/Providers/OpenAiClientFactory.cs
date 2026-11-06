using System.ClientModel;
using System.ClientModel.Primitives;
using OpenAI;
using RelaxKon.Auth.Application.Routing;

namespace RelaxKon.Auth.Application.Providers;

public sealed class OpenAiClientFactory(IHttpClientFactory clients)
{
    public OpenAiClientLease Create(ModelKey route, TimeSpan? timeout = null, string? endpoint = null)
    {
        var http = clients.CreateClient("AiProvider");
        var duration = timeout ?? TimeSpan.FromMinutes(5);
        http.Timeout = duration;
        try
        {
            var client = new OpenAIClient(new ApiKeyCredential(route.SupplierKey!.ApiKey!), new OpenAIClientOptions
            {
                Endpoint = new Uri(ResolveBaseUrl(endpoint ?? route.SupplierKey.BaseUrl)),
                Transport = new HttpClientPipelineTransport(http),
                NetworkTimeout = duration,
                RetryPolicy = new ClientRetryPolicy(maxRetries: 0) // ChatService owns route failover.
            });
            return new(http, client);
        }
        catch { http.Dispose(); throw; }
    }
    private static string ResolveBaseUrl(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return "https://api.openai.com/v1";
        var trimmed = configured.Trim().TrimEnd('/');
        if (!trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) && !trimmed.Contains("/v", StringComparison.Ordinal)) trimmed += "/v1";
        return trimmed;
    }
}
public sealed class OpenAiClientLease(HttpClient http, OpenAIClient client) : IDisposable
{
    public OpenAIClient Client { get; } = client;
    public void Dispose() => http.Dispose();
}
