using DashScope;
using DashScope.Models;
using Turbo.Auth.Controllers.AI.Chat.Models;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Models.AI.Chat;
using Message = DashScope.Models.Message;

namespace Turbo.Auth.Application.Chat;

public class AlibabaChatHandler: IChatHandler
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AlibabaChatHandler(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public HandlerType ProviderType => HandlerType.Alibaba;

    public async Task Chat(NoModelChatBody chatBody, ModelKey modelKey, HttpResponse response,
        CancellationToken cancellationToken)
    {
        var dScopeClient = new DashScopeClient(modelKey.SupplierKey!.ApiKey!, _httpClientFactory.CreateClient("AiProvider"));
        var request = new CompletionRequest();
        request.Model = modelKey.Model!;
        request.Input = ParseInput(chatBody);
        request.Parameters = ParseParameters(chatBody);
        await foreach (var res in dScopeClient.GenerationStreamAsync(request))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await response.WriteAsync(res.Output.Choices![0].Message.Content, cancellationToken);
        }

        await response.CompleteAsync();
    }

    private static CompletionInput ParseInput(NoModelChatBody chatBody)
    {
        var ms = new CompletionInput();
        ms.Messages = new List<Message>();
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
    private static CompletionParameters ParseParameters(NoModelChatBody chatBody)
    {
        return new CompletionParameters()
        {
            TopP = chatBody.TopP.HasValue? (float)chatBody.TopP : null,
            Temperature = chatBody.Temperature.HasValue?(float)chatBody.Temperature:null,
            ResultFormat = "message",
            IncrementalOutput = true
        };
    }
}
