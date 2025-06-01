namespace Turbo_Auth.Models.Ai.Chat;

public class ChatDisplayModel
{
    public ChatDisplayModel()
    {
        
    }
    public ChatDisplayModel(string modelName, string modelValue)
    {
        ModelName = modelName;
        ModelValue = modelValue;
    }

    public ChatDisplayModel(string modelValue)
    {
        ModelName = modelValue;
        ModelValue = modelValue;
    }
    public string? ModelName
    {
        get;
        set;
    }

    public string? ModelValue
    {
        get;
        set;
    }

    public bool Vision
    {
        get;
        set;
    }
}