using Betalgo.Ranul.OpenAI;
using Betalgo.Ranul.OpenAI.Managers;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Ai.Chat;

namespace Turbo_Auth.Handlers.Chat;

public class OpenAiChatHandler : IChatHandler
{
    
    public async Task Chat(NoModelChatBody chatBody, ModelKey modelKey,HttpResponse response)
    {
        var url = modelKey.SupplierKey!.BaseUrl!.Trim();
        var uri = new Uri(url);
        var path = uri.AbsolutePath;
        var subRoute = path.TrimStart('/');
        var baseUrl = uri.GetLeftPart(UriPartial.Authority);
        
        var option = new OpenAIOptions()
        {
            ApiKey = modelKey.SupplierKey!.ApiKey!,
            BaseDomain = baseUrl,
        };
        if (baseUrl.Contains("azure.com"))
        {
            option.ProviderType = ProviderType.Azure;
            option.DeploymentId = "jp-ai";
        }
        if (!subRoute.IsNullOrEmpty())
        {
            option.ApiVersion = subRoute;
        }
        var openAiService = new OpenAIService(option
        );
        
        var messages = TransferObject(chatBody.Messages!, chatBody.Vision);
        var completionResult = openAiService.ChatCompletion.CreateCompletionAsStream(new ChatCompletionCreateRequest
        {
            Messages = messages,
            Model = modelKey.Model,
            MaxCompletionTokens = chatBody.MaxCompletionTokens,
            TopP = FilterSpecial(chatBody.TopP,modelKey.Model),
            PresencePenalty = FilterSpecial(chatBody.PresencePenalty,modelKey.Model),
        });
        await foreach (var completion in completionResult)
        {
            if (completion.Successful)
            {
                if (completion.Choices.FirstOrDefault() == null) continue;
                if (completion.Choices.FirstOrDefault()?.Message == null) continue;
                if (completion.Choices.FirstOrDefault()?.Message.Content == null) continue;
                if (completion.Choices.FirstOrDefault()?.Message.Content!.Length > 0)
                {
                    await response.WriteAsync(completion.Choices.FirstOrDefault()?.Message.Content!);
                }
            }
            else
            {
                if (completion.Error == null)
                {
                    throw new Exception("Unknown Error");
                }

                await response.WriteAsync($"{completion.Error.Code}: {completion.Error.Message}");
            }
        }

        await response.CompleteAsync();
    }

    private static float? FilterSpecial(double? p,string model)
    {
        if (model.StartsWith("o", StringComparison.CurrentCultureIgnoreCase))
        {
            return null;
        }
        return (float?)p;
    }

    private static List<ChatMessage> TransferObject(IEnumerable<Message> messages,bool vision=false)
    {
        
        var ms = new List<ChatMessage>();
        foreach (var message in messages)
        {
            switch (message.Role!.ToLower()!)
            {
                case OpenAiRole.SystemRole:
                    ms.Add(ChatMessage.FromSystem(message.Content! as string));
                    break;
                case OpenAiRole.UserRole:
                    if (vision)
                    {
                        var mcl = new List<MessageContent>();
                        foreach (var vc in JsonConvert.DeserializeObject<VisionMessage>(JsonConvert.SerializeObject(message))!.Content)
                        {
                            if (vc.Type == "text")
                            {
                                mcl.Add(new MessageContent()
                                {
                                    Type = vc.Type!,
                                    Text = vc.Text,
                                });
                            }
                            else
                            {
                                mcl.Add(new MessageContent()
                                {
                                    Type = vc.Type!,
                                    ImageUrl = new MessageImageUrl()
                                    {
                                        Url = vc.VisionImage!.Url!,
                                        Detail = vc.VisionImage.Detail!
                                    }
                                });
                            }

                            
                        }
                        ms.Add(ChatMessage.FromUser(mcl));
                    }
                    else
                    {
                        ms.Add(ChatMessage.FromUser(message.Content! as string));
                    }
                    
                    break;
                case OpenAiRole.Assistant:
                    ms.Add(ChatMessage.FromAssistant(message.Content! as string));
                    break;
                default:
                    ms.Add(ChatMessage.FromUser(message.Content! as string));
                    break;
            }
        }
        return ms;
    }
}

public class OpenAiRole
{
    public const string SystemRole = "system";
    public const string Assistant = "assistant";
    public const string UserRole = "user";
}