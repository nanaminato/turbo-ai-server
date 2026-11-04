using System.Runtime.CompilerServices;
using Google.GenAI;
using Google.GenAI.Types;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;

namespace Turbo.Auth.Application.Chat;

public sealed class GoogleChatHandler(IHttpClientFactory clients) : IChatHandler
{
    public HandlerType ProviderType => HandlerType.Google;
    public async IAsyncEnumerable<string> Stream(ChatRequest request, ModelKey route, [EnumeratorCancellation] CancellationToken token)
    {
        var endpoint = route.SupplierKey?.BaseUrl?.TrimEnd('/');
        if (endpoint?.EndsWith("/v1beta", StringComparison.OrdinalIgnoreCase) == true) endpoint = endpoint[..^7];
        using var client = new Client(apiKey: route.SupplierKey!.ApiKey!,
            httpOptions: new HttpOptions
            {
                BaseUrl = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint,
                ApiVersion = "v1beta",
                Timeout = 300000,
                RetryOptions = new HttpRetryOptions { Attempts = 1 }
            },
            clientOptions: new ClientOptions { HttpClientFactory = () => clients.CreateClient("AiProvider") });
        var system = string.Join("\n", request.Messages.Where(message => message.Role is "system" or "developer").Select(message => message.Content.PlainText));
        var config = new GenerateContentConfig { SystemInstruction = string.IsNullOrEmpty(system) ? null : new Content { Parts = [new Part { Text = system }] } };
        var contents = request.Messages.Where(message => message.Role is not ("system" or "developer"))
            .Select(message => new Content { Role = message.Role == "assistant" ? "model" : "user", Parts = Parts(message.Content, request.Vision) }).ToList();
        var finished = false;
        await foreach (var update in client.Models.GenerateContentStreamAsync(route.Model!, contents, config, cancellationToken: token))
        {
            var candidate = update.Candidates?.FirstOrDefault();
            finished |= candidate?.FinishReason is not null;
            foreach (var part in candidate?.Content?.Parts ?? [])
                if (part.Thought != true && !string.IsNullOrEmpty(part.Text)) yield return part.Text;
        }
        if (!finished) throw new EndOfStreamException("Gemini stream ended without a finish reason.");
    }
    private static List<Part> Parts(ChatContent content, bool vision)
    {
        if (content.Text is not null) return [new Part { Text = content.Text }];
        return content.Parts.Where(part => part.Text is not null || vision).Select(part =>
        {
            if (part.Text is not null) return new Part { Text = part.Text };
            var url = part.ImageUrl!;
            if (!url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return new Part { FileData = new FileData { FileUri = url } };
            var separator = url.IndexOf(',');
            return new Part { InlineData = new Blob { MimeType = url[5..url.IndexOf(';')], Data = Convert.FromBase64String(url[(separator + 1)..]) } };
        }).ToList();
    }
}
