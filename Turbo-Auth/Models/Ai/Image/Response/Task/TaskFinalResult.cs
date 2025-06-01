using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Turbo_Auth.Models.Ai.Video;
using Turbo_Auth.Models.Ai.Video.Response;

namespace Turbo_Auth.Models.Ai.Image.Response.Task;

public class TaskFinalResult
{
    [JsonProperty("extra")]
    [Required]
    public TaskExtra? Extra
    {
        get;
        set;
    }

    [JsonProperty("images")]
    public List<TaskFinalImage>? Images
    {
        get;
        set;
    }

    [JsonProperty("videos")]
    [Required]
    public List<TaskFinalVideo>? Videos
    {
        get;
        set;
    }
}

public class TaskExtra
{
    [JsonProperty("seed")]
    public string? Seed
    {
        get;
        set;
    }
    [JsonProperty("enable_nsfw_detection")]
    public string? EnableNsfwDetection
    {
        get;
        set;
    }
}

public class TaskInfo
{
    [JsonProperty("task_id")]
    public string? TaskId
    {
        get;
        set;
    }

    [JsonProperty("status")]
    public string? Status
    {
        get;
        set;
    }

    [JsonProperty("reason")]
    public string? Reason
    {
        get;
        set;
    }

    [JsonProperty("task_type")]
    public string? TaskType
    {
        get;
        set;
    }

    [JsonProperty("eta")]
    public int Eta
    {
        get;
        set;
    }

    [JsonProperty("progress_percent")]
    public int ProgressPercent
    {
        get;
        set;
    }

    [JsonProperty("images")]
    public List<TaskFinalImage>? Images
    {
        get;
        set;
    }

    [JsonProperty("videos")]
    public List<TaskFinalVideo>? Videos
    {
        get;
        set;
    }
    
}