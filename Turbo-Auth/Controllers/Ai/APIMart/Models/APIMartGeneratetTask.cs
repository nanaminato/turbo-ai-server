using Newtonsoft.Json;

namespace Turbo_Auth.Controllers.Ai.APIMart.Models;

public class APIMartGeneratetTask
{
    public int? Code { get; set; }
    public List<APIMartTaskData>? Data { get; set; }
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