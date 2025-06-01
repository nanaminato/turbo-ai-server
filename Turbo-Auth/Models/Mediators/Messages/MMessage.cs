using Turbo_Auth.Models.ClientSyncs.Messages;

namespace Turbo_Auth.Models.Mediators.Messages;

public class MMessage
{
    public long HistoryDataId
    {
        get;
        set;
    }

    public int UserId
    {
        get;
        set;
    }
    public long DataId { get; set; }
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
        return $"HistoryDataId: {HistoryDataId}, UserId: {UserId}, DataId: {DataId}, Role: {Role ?? "null"}, Content: {Content ?? "null"}, FileList: {FileList?.Count ?? 0}, ShowType: {ShowType}, Finish: {Finish}, Model: {Model ?? "null"}";
    }

}