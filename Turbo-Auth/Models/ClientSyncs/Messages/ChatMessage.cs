using System.ComponentModel.DataAnnotations;

namespace Turbo_Auth.Models.ClientSyncs.Messages;

public class ChatMessage
{
    [Key]
    public long ChatMessageId
    {
        get;
        set;
    }
    public long DataId
    {
        get;
        set;
    }
    public long ChatHistoryId
    {
        get;
        set;
    }

    public ChatHistory? ChatHistory
    {
        get;
        set;
    }

    public string? Role
    {
        get;
        set;
    }

    public string? Content
    {
        get;
        set;
    }
    public List<FileAdds>? FileList { get; set; } 
    
    public int ShowType
    {
        get;
        set;
    }

    public bool Finish
    {
        get;
        set;
    }

    public string? Model
    {
        get;
        set;
    }

    
    public override string ToString()
    {
        return $"ChatMessageId: {ChatMessageId}, Role: {Role}, Content: {Content}, DataId: {DataId}, ShowType: {ShowType}, Finish: {Finish}, Model: {Model}, ChatHistoryId: {ChatHistoryId}";
    }

}