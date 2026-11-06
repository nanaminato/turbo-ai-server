using Newtonsoft.Json;
namespace RelaxKon.Auth.Contracts.Media;

public sealed record SpeechResponse(string Base64, string Type);
public sealed record AudioWordResponse(string Word, double Start, double End);
public sealed record AudioSegmentResponse(int Id, int Seek, double Start, double End, string Text,
    IReadOnlyList<int> Tokens, float Temperature,
    [property: JsonProperty("avg_logprob")] float AverageLogProbability,
    [property: JsonProperty("compression_ratio")] float CompressionRatio,
    [property: JsonProperty("no_speech_prob")] float NoSpeechProbability);
public sealed record AudioTextResponse(string Text, string? Language = null, double? Duration = null,
    IReadOnlyList<AudioSegmentResponse>? Segments = null, IReadOnlyList<AudioWordResponse>? Words = null);
public sealed record ImageResultResponse(string? Url, [property: JsonProperty("b64_json")] string? Base64,
    [property: JsonProperty("revised_prompt")] string? RevisedPrompt);
public sealed record ImageUsageResponse([property: JsonProperty("input_tokens")] long InputTokens,
    [property: JsonProperty("output_tokens")] long OutputTokens,
    [property: JsonProperty("total_tokens")] long TotalTokens);
public sealed record ImageGenerationResponse(long Created, IReadOnlyList<ImageResultResponse> Data,
    string? Background, [property: JsonProperty("output_format")] string? OutputFormat, string? Quality,
    string? Size, ImageUsageResponse? Usage);
