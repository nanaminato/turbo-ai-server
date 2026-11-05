#pragma warning disable OPENAI001 // Official SDK marks GPT image options experimental; contained in this adapter.
using OpenAI.Audio;
using OpenAI.Images;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Contracts.Media;

namespace Turbo.Auth.Application.Media;

public interface IMediaService
{
    Task<SpeechResponse> Speech(SpeechRequest request, CancellationToken token);
    Task<AudioTextResponse> Transcribe(AudioTranscriptionRequest request, CancellationToken token);
    Task<AudioTextResponse> Translate(AudioTranslationRequest request, CancellationToken token);
    Task<ImageGenerationResponse> Image(ImageGenerationRequest request, bool dalle, CancellationToken token);
}
public sealed class MediaService(QuickModel routes, OpenAiClientFactory clients) : IMediaService
{
    public async Task<SpeechResponse> Speech(SpeechRequest request, CancellationToken token)
    {
        var route = routes.GetModelAndKey(request.Model);
        using var lease = clients.Create(route);
        var bytes = await lease.Client.GetAudioClient(route.Model!).GenerateSpeechAsync(request.Input,
            new GeneratedSpeechVoice(request.Voice), new SpeechGenerationOptions
            {
                ResponseFormat = new GeneratedSpeechFormat(request.ResponseFormat),
                SpeedRatio = request.Speed
            }, token);
        return new(Convert.ToBase64String(bytes.Value.ToArray()), request.ResponseFormat);
    }
    public async Task<AudioTextResponse> Transcribe(AudioTranscriptionRequest request, CancellationToken token)
    {
        var route = routes.GetModelAndKey(request.Model);
        using var lease = clients.Create(route);
        await using var file = new MemoryStream(Convert.FromBase64String(request.File));
        var result = await lease.Client.GetAudioClient(route.Model!).TranscribeAudioAsync(file, "audio." + request.Suffix,
            new AudioTranscriptionOptions
            {
                Language = request.Language,
                Prompt = request.Prompt,
                Temperature = request.Temperature,
                ResponseFormat = request.ResponseFormat is null ? (AudioTranscriptionFormat?)null : new AudioTranscriptionFormat(request.ResponseFormat)
            }, token);
        return new(result.Value.Text, result.Value.Language, result.Value.Duration?.TotalSeconds,
            Segments(result.Value.Segments), result.Value.Words?.Select(word => new AudioWordResponse(word.Word, word.StartTime.TotalSeconds, word.EndTime.TotalSeconds)).ToList());
    }
    public async Task<AudioTextResponse> Translate(AudioTranslationRequest request, CancellationToken token)
    {
        var route = routes.GetModelAndKey(request.Model);
        using var lease = clients.Create(route);
        await using var file = new MemoryStream(Convert.FromBase64String(request.File));
        var result = await lease.Client.GetAudioClient(route.Model!).TranslateAudioAsync(file, "audio." + request.Suffix,
            new AudioTranslationOptions
            {
                Prompt = request.Prompt,
                Temperature = request.Temperature,
                ResponseFormat = request.ResponseFormat is null ? (AudioTranslationFormat?)null : new AudioTranslationFormat(request.ResponseFormat)
            }, token);
        return new(result.Value.Text, result.Value.Language, result.Value.Duration?.TotalSeconds, Segments(result.Value.Segments));
    }
    public async Task<ImageGenerationResponse> Image(ImageGenerationRequest request, bool dalle, CancellationToken token)
    {
        ImageGenerationPolicy.RejectObsoleteModel(request.Model);
        // This endpoint requires synchronous OpenAI-compatible image responses.
        // APIMart task receipts must go through the dedicated asynchronous endpoint.
        var incompatible = routes.GetModelKeys(request.Model)
            .Where(route => route.SupplierKey?.RequestIdentifier != (int)HandlerType.Openai)
            .Select(route => route.RouteId).ToHashSet();
        var route = routes.GetModelAndKey(request.Model, incompatible);
        using var lease = clients.Create(route);
        var payload = ImageGenerationPolicy.Build(request, route.Model!, dalle);
        using var content = System.ClientModel.BinaryContent.Create(BinaryData.FromObjectAsJson(payload));
        var result = await lease.Client.GetImageClient(route.Model!).GenerateImagesAsync(content,
            new System.ClientModel.Primitives.RequestOptions { CancellationToken = token });
        return ImageGenerationPolicy.Response(result.GetRawResponse().Content, payload);
    }

    private static IReadOnlyList<AudioSegmentResponse>? Segments(IReadOnlyList<TranscribedSegment>? segments) => segments?.Select(segment =>
        new AudioSegmentResponse(segment.Id, segment.SeekOffset, segment.StartTime.TotalSeconds, segment.EndTime.TotalSeconds,
            segment.Text, segment.TokenIds.ToArray(), segment.Temperature, segment.AverageLogProbability,
            segment.CompressionRatio, segment.NoSpeechProbability)).ToList();
}

#pragma warning restore OPENAI001
