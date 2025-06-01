using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo_Auth.Models.ClientSyncs.Messages;

public class FileAdds
{
    [Key]
    public long FileAddsId
    {
        get;
        set;
    }
    public string? FileName
    {
        set;
        get;
    }

    public string? FileType
    {
        get;
        set;
    }

    public long FileSize
    {
        get;
        set;
    }

    public string? FileContent
    {
        get;
        set;
    }

    public string? ParsedContent
    {
        get;
        set;
    }
    public long ChatMessageId { get;set;}
    [ForeignKey("ChatMessageId")]
    public ChatMessage? ChatMessage{get;set;}
}