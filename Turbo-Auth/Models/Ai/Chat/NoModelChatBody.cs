using System.Text;
using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Chat;
public class NoModelChatBody
{
    [JsonProperty("messages")]
    public Message[]? Messages { get; set; }
    
    [JsonProperty("model")]
    public string? Model { get; set; }
    
    [JsonProperty("frequency_penalty")]
    public double? FrequencyPenalty { get; set; }
    [JsonProperty("max_completion_tokens")]
    public int? MaxCompletionTokens { get; set; }
    
    [JsonProperty("presence_penalty")]
    public double? PresencePenalty { get; set; }
    
    [JsonProperty("stream")]
    public bool Stream { get; set;}
    
    [JsonProperty("temperature")] 
    public double? Temperature { get; set;}

    [JsonProperty("top_p")] 
    public double? TopP {get;set;}

    [JsonProperty("vision")]
    public bool Vision
    {
        get;
        set;
    }
    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var message in Messages!)
        {
            sb.AppendLine(message.ToString());
        }
        return $"ChatBody: Messages=\n{sb}, " +
               $"Model={Model}, FrequencyPenalty={FrequencyPenalty}, MaxCompletionTokens={MaxCompletionTokens}, PresencePenalty={PresencePenalty}, Stream={Stream}, Temperature={Temperature}, TopP={TopP}";
    }
}

public class VisionMessage
{
    [JsonProperty("role")]
    public string? Role { get; set; }

    [JsonProperty("content")]
    public List<VisionContent>? Content { get; set; }

    public override string ToString()
    {
        return $"Message: Role={Role}, Content={Content}";
    }
}

public class Message
{
    [JsonProperty("role")]
    public string? Role { get; set; }

    [JsonProperty("content")]
    public dynamic? Content { get; set; }

    public override string ToString()
    {
        return $"Message: Role={Role}, Content={Content}";
    }
}

public class VisionContent
{
    [JsonProperty("type")]
    public string? Type
    {
        get;
        set;
    }
    [JsonProperty("text")]
    public string? Text
    {
        get;
        set;
    }
    [JsonProperty("image_url")]
    public VisionImage? VisionImage
    {
        get;
        set;
    }

    public override string ToString()
    {
        return $"type {Type} text {Text} img {VisionImage}";
    }
}

public class VisionImage
{
    [JsonProperty("url")]
    public string? Url
    {
        get;
        set;
    }
    [JsonProperty("detail")]
    public string? Detail
    {
        get;
        set;
    }

    public override string ToString()
    {
        return $"url: {Url} detail: {Detail}";
    }
}