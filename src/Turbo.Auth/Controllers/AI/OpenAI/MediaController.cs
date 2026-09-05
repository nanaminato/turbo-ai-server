using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Controllers.AI.OpenAI.Models;
using tryAGI.OpenAI;

namespace Turbo.Auth.Controllers.AI.OpenAI;

/// <summary>
/// OpenAI 多模态端点：TTS、Whisper 转写/翻译、DALL·E / gpt-image 文生图。
/// 基于 tryAGI.OpenAI 4.2.10。
/// </summary>
[ApiController]
[Authorize("vip")]
[Route("api/[controller]")]
public class MediaController : Controller
{
    private readonly QuickModel _quickModel;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MediaController> _logger;

    public MediaController(
        QuickModel quickModel,
        IHttpClientFactory httpClientFactory,
        ILogger<MediaController> logger
    )
    {
        _quickModel = quickModel;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private OpenAiClient BuildClient(ModelKey modelKey, TimeSpan? timeout = null)
    {
        var baseUrl = ResolveBaseUrl(modelKey.SupplierKey?.BaseUrl);
        var apiKey = modelKey.SupplierKey!.ApiKey!;
        var http = _httpClientFactory.CreateClient("AiProvider");
        if (timeout.HasValue)
        {
            http.Timeout = timeout.Value;
        }
        return new OpenAiClient(apiKey, baseUri: new Uri(baseUrl));
    }

    [HttpPost("tts")]
    public async Task<IActionResult> TTS(AudioCreateSpeechRequest speechRequest,
        CancellationToken cancellationToken)
    {
        var modelKey = _quickModel.GetModelAndKey(speechRequest.Model!);
        var client = BuildClient(modelKey);

        // 便捷重载：直接返回 byte[]，无需处理 SSE 流式分片。
        var audio = await client.Audio.CreateSpeechAsync(
            model: speechRequest.Model!,
            input: speechRequest.Input!,
            voice: ParseVoice(speechRequest.Voice!),
            responseFormat: ParseSpeechResponseFormat(speechRequest.ResponseFormat),
            speed: speechRequest.Speed,
            cancellationToken: cancellationToken);

        var format = (speechRequest.ResponseFormat ?? "mp3").ToLowerInvariant();
        return Ok(new
        {
            base64 = Convert.ToBase64String(audio),
            type = format,
        });
    }

    [HttpPost("whisper-translate")]
    public async Task<IActionResult> WhisperTranslate(OpenAiTranslationRequest request,
        CancellationToken cancellationToken)
    {
        var modelKey = _quickModel.GetModelAndKey(request.Model!);
        var client = BuildClient(modelKey);

        var fileBytes = Convert.FromBase64String(request.File!);
        var req = new CreateTranslationRequest
        {
            Model = request.Model!,
            File = fileBytes,
            Filename = $"audio.{request.Suffix ?? "mp3"}",
            Prompt = request.Prompt,
            ResponseFormat = ParseTranslationResponseFormat(request.ResponseFormat),
            Temperature = ToFloat(request.Temperature),
        };
        var translateResult = await client.Audio.CreateTranslationAsync(req, cancellationToken: cancellationToken);
        return Ok(translateResult);
    }

    [HttpPost("whisper-transcription")]
    public async Task<IActionResult> WhisperTranscription(OpenAiTranscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var modelKey = _quickModel.GetModelAndKey(request.Model!);
        var client = BuildClient(modelKey);

        var fileBytes = Convert.FromBase64String(request.File!);
        var req = new CreateTranscriptionRequest
        {
            Model = request.Model!,
            File = fileBytes,
            Filename = $"audio.{request.Suffix ?? "mp3"}",
            Prompt = request.Prompt,
            ResponseFormat = ParseAudioResponseFormat(request.ResponseFormat),
            Language = request.Language,
            Temperature = ToFloat(request.Temperature),
        };
        var transcriptionResult = await client.Audio.CreateTranscriptionAsync(req, cancellationToken: cancellationToken);
        return Ok(transcriptionResult);
    }

    [HttpPost("dall-e")]
    public async Task<IActionResult> Dall_E(GPTImageCreateRequest createImage,
        CancellationToken cancellationToken)
    {
        var modelKey = _quickModel.GetModelAndKey(createImage.Model!);
        var client = BuildClient(modelKey, TimeSpan.FromMinutes(5));

        // dall-e-3 强制 n=1
        var n = createImage.Model == "dall-e-3" ? 1 : createImage.N;
        var req = new CreateImageRequest
        {
            Prompt = createImage.Prompt!,
            Model = createImage.Model!,
            N = n,
            Quality = ParseImageQuality(createImage.Quality),
            ResponseFormat = ParseImageResponseFormat(createImage.ResponseFormat),
            Size = ParseImageSize(createImage.Size),
            Style = createImage.Style is null ? null : ParseImageStyle(createImage.Style),
        };
        var imageResult = await client.Images.CreateImageAsync(req, cancellationToken: cancellationToken);
        _logger.LogInformation("dall-e image created");
        return Ok(imageResult);
    }

    [HttpPost("gpt-image")]
    public async Task<IActionResult> Gpt_image(GPTImageCreateRequest createImage,
        CancellationToken cancellationToken)
    {
        var modelKey = _quickModel.GetModelAndKey(createImage.Model!);
        var client = BuildClient(modelKey, TimeSpan.FromMinutes(5));

        var req = new CreateImageRequest
        {
            Prompt = createImage.Prompt!,
            Model = createImage.Model!,
            N = createImage.N,
            Background = createImage.Background is null ? null : ParseImageBackground(createImage.Background),
            Moderation = createImage.Moderation is null ? null : ParseImageModeration(createImage.Moderation),
            Quality = ParseImageQuality(createImage.Quality),
            ResponseFormat = ParseImageResponseFormat(createImage.ResponseFormat),
            OutputFormat = createImage.OutputFormat is null ? null : ParseImageOutputFormat(createImage.OutputFormat),
            Size = ParseImageSize(createImage.Size),
        };
        var imageResult = await client.Images.CreateImageAsync(req, cancellationToken: cancellationToken);
        _logger.LogInformation("gpt-image created");
        return Ok(imageResult);
    }

    // ---- helpers ----

    private static string ResolveBaseUrl(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return "https://api.openai.com/v1";
        }
        var trimmed = configured.Trim().TrimEnd('/');
        if (!trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Contains("/v", StringComparison.Ordinal))
        {
            trimmed += "/v1";
        }
        return trimmed;
    }

    private static float? ToFloat(double? value)
    {
        if (!value.HasValue) return null;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value)) return null;
        return Convert.ToSingle(value.Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 把字符串 voice 名转换为 tryAGI 的 <see cref="VoiceIdsOrCustomVoice"/>。
    /// 内置 voice 走枚举路径；其他值原样作为字符串透传（支持自定义 voice id）。
    /// VoiceIdsOrCustomVoice 有 FromShared(VoiceIdsShared?) 工厂；自定义字符串需先
    /// 包成 VoiceIdsShared.FromVoiceIdsSharedVariant1(...)，再利用 op_Implicit 隐式转换。
    /// </summary>
    private static VoiceIdsOrCustomVoice ParseVoice(string voice)
    {
        var v = voice.Trim().ToLowerInvariant();
        return v switch
        {
            "alloy" => (VoiceIdsShared)VoiceIdsSharedEnum.Alloy,
            "ash" => (VoiceIdsShared)VoiceIdsSharedEnum.Ash,
            "ballad" => (VoiceIdsShared)VoiceIdsSharedEnum.Ballad,
            "cedar" => (VoiceIdsShared)VoiceIdsSharedEnum.Cedar,
            "coral" => (VoiceIdsShared)VoiceIdsSharedEnum.Coral,
            "echo" => (VoiceIdsShared)VoiceIdsSharedEnum.Echo,
            "marin" => (VoiceIdsShared)VoiceIdsSharedEnum.Marin,
            "sage" => (VoiceIdsShared)VoiceIdsSharedEnum.Sage,
            "shimmer" => (VoiceIdsShared)VoiceIdsSharedEnum.Shimmer,
            "verse" => (VoiceIdsShared)VoiceIdsSharedEnum.Verse,
            // 自定义 voice：原样字符串透传（包成 VoiceIdsShared 再隐式转换到 VoiceIdsOrCustomVoice）
            _ => (VoiceIdsOrCustomVoice)VoiceIdsShared.FromVoiceIdsSharedVariant1(v),
        };
    }

    private static CreateSpeechRequestResponseFormat? ParseSpeechResponseFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return null;
        return format.Trim().ToLowerInvariant() switch
        {
            "mp3" => CreateSpeechRequestResponseFormat.Mp3,
            "opus" => CreateSpeechRequestResponseFormat.Opus,
            "aac" => CreateSpeechRequestResponseFormat.Aac,
            "flac" => CreateSpeechRequestResponseFormat.Flac,
            "wav" => CreateSpeechRequestResponseFormat.Wav,
            "pcm" => CreateSpeechRequestResponseFormat.Pcm,
            _ => null,
        };
    }

    private static AudioResponseFormat? ParseAudioResponseFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return null;
        return format.Trim().ToLowerInvariant() switch
        {
            "json" => AudioResponseFormat.Json,
            "text" => AudioResponseFormat.Text,
            "srt" => AudioResponseFormat.Srt,
            "verbose_json" => AudioResponseFormat.VerboseJson,
            "diarized_json" => AudioResponseFormat.DiarizedJson,
            "vtt" => AudioResponseFormat.Vtt,
            _ => null,
        };
    }

    private static CreateTranslationRequestResponseFormat? ParseTranslationResponseFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return null;
        return format.Trim().ToLowerInvariant() switch
        {
            "json" => CreateTranslationRequestResponseFormat.Json,
            "text" => CreateTranslationRequestResponseFormat.Text,
            "srt" => CreateTranslationRequestResponseFormat.Srt,
            "verbose_json" => CreateTranslationRequestResponseFormat.VerboseJson,
            "vtt" => CreateTranslationRequestResponseFormat.Vtt,
            _ => null,
        };
    }

    private static CreateImageRequestQuality? ParseImageQuality(string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return null;
        return q.Trim().ToLowerInvariant() switch
        {
            "standard" => CreateImageRequestQuality.Standard,
            "hd" => CreateImageRequestQuality.Hd,
            "low" => CreateImageRequestQuality.Low,
            "medium" => CreateImageRequestQuality.Medium,
            "high" => CreateImageRequestQuality.High,
            "auto" => CreateImageRequestQuality.Auto,
            _ => null,
        };
    }

    private static CreateImageRequestResponseFormat? ParseImageResponseFormat(string? f)
    {
        if (string.IsNullOrWhiteSpace(f)) return null;
        return f.Trim().ToLowerInvariant() switch
        {
            "url" => CreateImageRequestResponseFormat.Url,
            "b64_json" => CreateImageRequestResponseFormat.B64Json,
            _ => null,
        };
    }

    private static CreateImageRequestOutputFormat? ParseImageOutputFormat(string? f)
    {
        if (string.IsNullOrWhiteSpace(f)) return null;
        return f.Trim().ToLowerInvariant() switch
        {
            "png" => CreateImageRequestOutputFormat.Png,
            "jpeg" => CreateImageRequestOutputFormat.Jpeg,
            "webp" => CreateImageRequestOutputFormat.Webp,
            _ => null,
        };
    }

    private static CreateImageRequestSize? ParseImageSize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return s.Trim().ToLowerInvariant() switch
        {
            "256x256" => CreateImageRequestSize.x256x256,
            "512x512" => CreateImageRequestSize.x512x512,
            "1024x1024" => CreateImageRequestSize.x1024x1024,
            "1024x1536" => CreateImageRequestSize.x1024x1536,
            "1024x1792" => CreateImageRequestSize.x1024x1792,
            "1536x1024" => CreateImageRequestSize.x1536x1024,
            "1792x1024" => CreateImageRequestSize.x1792x1024,
            "auto" => CreateImageRequestSize.Auto,
            _ => null,
        };
    }

    private static CreateImageRequestStyle? ParseImageStyle(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return s.Trim().ToLowerInvariant() switch
        {
            "vivid" => CreateImageRequestStyle.Vivid,
            "natural" => CreateImageRequestStyle.Natural,
            _ => null,
        };
    }

    private static CreateImageRequestBackground? ParseImageBackground(string? b)
    {
        if (string.IsNullOrWhiteSpace(b)) return null;
        return b.Trim().ToLowerInvariant() switch
        {
            "transparent" => CreateImageRequestBackground.Transparent,
            "opaque" => CreateImageRequestBackground.Opaque,
            "auto" => CreateImageRequestBackground.Auto,
            _ => null,
        };
    }

    private static CreateImageRequestModeration? ParseImageModeration(string? m)
    {
        if (string.IsNullOrWhiteSpace(m)) return null;
        return m.Trim().ToLowerInvariant() switch
        {
            "low" => CreateImageRequestModeration.Low,
            "auto" => CreateImageRequestModeration.Auto,
            _ => null,
        };
    }
}