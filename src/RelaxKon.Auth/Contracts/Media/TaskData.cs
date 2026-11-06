using Newtonsoft.Json;

namespace RelaxKon.Auth.Contracts.Media;

public class TaskData
{
    public APIMartTaskError? Error { get; set; }
    public decimal? Cost { get; set; }
    [JsonProperty("credits_cost")] public decimal? CreditsCost { get; set; }
    public APIMartTaskUsage? Usage { get; set; }
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("status")]
    public string Status { get; set; } = "";

    [JsonProperty("progress")]
    public int Progress { get; set; }

    [JsonProperty("result")]
    public TaskResult? Result { get; set; }

    [JsonProperty("created")]
    public long Created { get; set; } // Unix 时间戳

    [JsonProperty("completed")]
    public long Completed { get; set; } // Unix 时间戳

    [JsonProperty("estimated_time")]
    public int EstimatedTime { get; set; }

    [JsonProperty("actual_time")]
    public int ActualTime { get; set; }
}
public sealed class APIMartTaskUsage { [JsonProperty("svg_usage")] public APIMartSvgUsage? SvgUsage { get; set; } [JsonProperty("input_tokens")] public long InputTokens { get; set; } [JsonProperty("output_tokens")] public long OutputTokens { get; set; } [JsonProperty("total_tokens")] public long TotalTokens { get; set; } }

public sealed class APIMartSvgUsage { public string? Model { get; set; } [JsonProperty("input_tokens")] public long InputTokens { get; set; } [JsonProperty("output_tokens")] public long OutputTokens { get; set; } [JsonProperty("reasoning_tokens")] public long ReasoningTokens { get; set; } }
