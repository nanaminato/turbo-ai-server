using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Response.APIMartTask;

public class APIMartGeneratetTask
{
    public string? Code { get; set; }
    public APIMartTaskData? Data { get; set; }
    public APIMartTaskError? Error { get; set; }
}

public class APIMartTaskData
{
    public string Status
    {
        get;
        set;
    }
    [JsonProperty("task_id")]
    public string TaskId
    {
        get;
        set;
    }
}
public class APIMartTaskError
{
    public string Code { get; set; }
    public string Message { get; set; }
    public string Type { get; set; }
}