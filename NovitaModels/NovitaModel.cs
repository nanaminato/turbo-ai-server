namespace NovitaModels;

public class NovitaModel
{
    public int ModelId
    {
        get;
        set;
    }
    public string? Model
    {
        get;
        set;
    }

    public string? Cover
    {
        get;
        set;
    }

    public string? Type
    {
        get;
        set;
    }

    public bool Nsfw
    {
        get;
        set;
    }

    public bool Sdxl
    {
        get;
        set;
    }

    public override string ToString()
    {
        return $"ModelId: {ModelId}, Model: {Model}, Cover: {Cover}, Type: {Type}, Nsfw: {Nsfw}, Sdxl: {Sdxl}";
    }

}