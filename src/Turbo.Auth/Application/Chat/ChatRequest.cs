using System.ComponentModel.DataAnnotations;
﻿using System.Text;
using Newtonsoft.Json;

namespace Turbo.Auth.Application.Chat;
public class ChatRequest
{
    [Required, MinLength(1)]
    [JsonProperty("messages")]
    public Message[]? Messages { get; set; }

    [JsonProperty("model")]
    public string? Model { get; set; }

    [JsonProperty("stream")]
    public bool Stream { get; set; }

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
               $"Model={Model}, Stream={Stream}";
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

    public override string ToString()
    {
        return $"url: {Url}";
    }
}