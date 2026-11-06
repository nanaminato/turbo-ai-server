using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RelaxKon.Auth.Models.Accounts;

namespace RelaxKon.Auth.Models.Sync.Messages;

public class ChatHistory
{
    [Key]
    public long ChatHistoryId
    {
        get;
        set;
    }

    public int UserId
    {
        get;
        set;
    }

    [ForeignKey("UserId")]
    public Account? Account
    {
        get;
        set;
    }
    
    public long DataId
    {
        get;
        set;
    }
    public string? Title
    {
        get;
        set;
    }

    public IList<ChatMessage>? ChatMessages
    {
        get;
        set;
    }
    public override string ToString()
    {
        return $"ChatHistoryId: {ChatHistoryId}, UserId: {UserId}, Title: {Title}, DataId: {DataId}";
    }

}