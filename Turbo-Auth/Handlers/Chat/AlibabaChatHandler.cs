using DashScope;
using DashScope.Models;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Ai.Chat;
using Message = DashScope.Models.Message;

namespace Turbo_Auth.Handlers.Chat;

public class AlibabaChatHandler: IChatHandler
{
    public async Task Chat(NoModelChatBody chatBody, ModelKey modelKey, HttpResponse response)
    {
        var dScopeClient = new DashScopeClient(modelKey.SupplierKey!.ApiKey!, new HttpClient());
        var request = new CompletionRequest();
        request.Model = modelKey.Model!;
        request.Input = ParseInput(chatBody);
        request.Parameters = ParseParameters(chatBody);
        await foreach (var res in dScopeClient.GenerationStreamAsync(request))
        {
            await response.WriteAsync(res.Output.Choices![0].Message.Content);
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
