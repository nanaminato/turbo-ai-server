using Betalgo.Ranul.OpenAI;
using Betalgo.Ranul.OpenAI.Contracts.Requests.Image;
using Betalgo.Ranul.OpenAI.Managers;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Ai.Image.Request;
using Turbo_Auth.Models.Ai.Image.Request.GPTImage;
using Turbo_Auth.Models.Ai.Media.STT;
using Turbo_Auth.Models.Suppliers;

namespace Turbo_Auth.Controllers.Ai;

[ApiController]
[Authorize("vip")]
[Route("api/[controller]")]
public class MediaController : Controller
{
    private QuickModel _quickModel;
    private readonly ILogger<MediaController> _logger;

    public MediaController(
        QuickModel quickModel,
        ILogger<MediaController> logger
    )
    {
        _quickModel = quickModel;
        _logger = logger;
    }

    [HttpPost("tts")]
    public async Task<IActionResult> TTS(AudioCreateSpeechRequest speechRequest)
    {
        var modelKey = _quickModel.GetModelAndKey(speechRequest.Model);
        var openaiService = new OpenAIService(new OpenAIOptions()
        {
            ApiKey = modelKey!.SupplierKey!.ApiKey!,
            BaseDomain = modelKey.SupplierKey.BaseUrl!
        });
        var ttsResult = 
            await openaiService.Audio.CreateSpeech<byte[]>(speechRequest);
        return Ok(new 
        {
            base64 = Convert.ToBase64String(ttsResult.Data!),
            type = speechRequest.ResponseFormat
        });
    }

    [HttpPost("whisper-translate")]
    public async Task<IActionResult> WhisperTranslate(OpenAiTranslationRequest request)
    {
        var modelKey = _quickModel.GetModelAndKey(request.Model!);
        var openaiService = new OpenAIService(new OpenAIOptions()
        {
            ApiKey = modelKey!.SupplierKey!.ApiKey!,
            BaseDomain = modelKey.SupplierKey.BaseUrl!
        });
        var audioRequest = Transfer(request);
        // Console.WriteLine(audioRequest);
        var translateResult = await openaiService.Audio.CreateTranslation(audioRequest);
        return Ok(translateResult);
    }
    [HttpPost("whisper-transcription")]
    public async Task<IActionResult> WhisperTranscription(OpenAiTranscriptionRequest request)
    {
        var modelKey = _quickModel.GetModelAndKey(request.Model!);
        var openaiService = new OpenAIService(new OpenAIOptions()
        {
            ApiKey = modelKey!.SupplierKey!.ApiKey!,
            BaseDomain = modelKey.SupplierKey.BaseUrl!
        });
        var audioRequest = Transfer(request);
        // Console.WriteLine(audioRequest);
        var transcriptionResult = await openaiService.Audio.CreateTranscription(audioRequest);
        return Ok(transcriptionResult);
    }
    private AudioCreateTranscriptionRequest Transfer(OpenAiTranslationRequest request)
    {
        
        var audioRequest = new AudioCreateTranscriptionRequest()
        {
            Model = request.Model!,
            Prompt = request.Prompt,
            Temperature = request.Temperature,
            ResponseFormat = request.ResponseFormat,
            FileName = DateTime.Now.ToLongTimeString()+"."+request.Suffix,
            File = Convert.FromBase64String(request.File!)
        };
        return audioRequest;
    }
    private AudioCreateTranscriptionRequest Transfer(OpenAiTranscriptionRequest request)
    {
        var audioRequest = new AudioCreateTranscriptionRequest()
        {
            Model = request.Model!,
            Prompt = request.Prompt,
            Temperature = request.Temperature,
            ResponseFormat = request.ResponseFormat,
            Language = request.Language,
            FileName = DateTime.Now.ToLongTimeString()+"."+request.Suffix,
            File = Convert.FromBase64String(request.File!)
        };
        return audioRequest;
    }

    [HttpPost("dall-e")]
    public async Task<IActionResult> Dall_E(GPTImageCreateRequest createImage)
    {
        var modelKey = _quickModel.GetModelAndKey(createImage.Model!);
        var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5); // 设置超时
        var openaiService = new OpenAIService(new OpenAIOptions()
        {
            ApiKey = modelKey!.SupplierKey!.ApiKey!,
            BaseDomain = modelKey.SupplierKey.BaseUrl!
        }, httpClient);
        var request = new CreateImageRequest()
        {
            Prompt = createImage.Prompt!,
            Model = createImage.Model,
            N = createImage.N,
            OutputFormat = createImage.OutputFormat,
            Quality = createImage.Quality,
            ResponseFormat = createImage.ResponseFormat,
            Size = createImage.Size,
        };
        if (createImage.Model == "dall-e-3")
        {
            createImage.Style = createImage.Style;
            createImage.N = 1;
        }
        var imageResult = await openaiService
            .Image.CreateImage(request);
        _logger.LogInformation(imageResult.ToString());
        return Ok(imageResult);
    }
    [HttpPost("gpt-image")]
    public async Task<IActionResult> Gpt_image(GPTImageCreateRequest createImage)
    {
        var modelKey = _quickModel.GetModelAndKey(createImage.Model!);
        var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5); // 设置超时
        var openaiService = new OpenAIService(new OpenAIOptions()
        {
            ApiKey = modelKey!.SupplierKey!.ApiKey!,
            BaseDomain = modelKey.SupplierKey.BaseUrl!
        }, httpClient);
        var imageResult = await openaiService
            .Image.CreateImage(
                new CreateImageRequest()
                {
                    Prompt = createImage.Prompt!,
                    Background =  createImage.Background,
                    Model = createImage.Model,
                    Moderation =  createImage.Moderation,
                    N = createImage.N,
                    OutputFormat = createImage.OutputFormat,
                    Quality = createImage.Quality,
                    Size = createImage.Size
                });
        // Console.WriteLine(imageResult);
        _logger.LogInformation(imageResult.ToString());
        return Ok(imageResult);
    }
}