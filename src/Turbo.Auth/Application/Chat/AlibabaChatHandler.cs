using DashScope;
using DashScope.Models;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Models.AI.Chat;
using DashScopeMessage = DashScope.Models.Message;

namespace Turbo.Auth.Application.Chat;

public class AlibabaChatHandler: IChatHandler
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AlibabaChatHandler(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public HandlerType ProviderType => HandlerType.Alibaba;

    public async IAsyncEnumerable<string> Stream(ChatRequest chatBody, ModelKey modelKey,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var dScopeClient = new DashScopeClient(modelKey.SupplierKey!.ApiKey!, _httpClientFactory.CreateClient("AiProvider"));
        var request = new CompletionRequest();
        request.Model = modelKey.Model!;
        request.Input = ParseInput(chatBody);
        request.Parameters = ParseParameters(chatBody);
        var finished = false;
        await foreach (var res in dScopeClient.GenerationStreamAsync(request, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var choice = res.Output?.Choices?.FirstOrDefault();
            var reason = choice?.FinishReason ?? res.Output?.FinishReason;
            finished |= !string.IsNullOrEmpty(reason) && reason != "null";
            if (choice?.Message?.Content is { Length: > 0 } text) yield return text;
        }


        if (!finished) throw new EndOfStreamException("DashScope stream ended without a finish reason.");
    }

    private static CompletionInput ParseInput(ChatRequest chatBody)
    {
        var ms = new CompletionInput();
        ms.Messages = new List<DashScopeMessage>();
        foreach (var message in chatBody.Messages!)
        {
            ms.Messages.Add(new ()
            {
                Content = message.Content!,
                Role = message.Role!
            });
        }

        return ms;
    }
    private static CompletionParameters ParseParameters(ChatRequest chatBody)
    {
        return new CompletionParameters()
        {
            ResultFormat = "message",
            IncrementalOutput = true
        };
    }
}
