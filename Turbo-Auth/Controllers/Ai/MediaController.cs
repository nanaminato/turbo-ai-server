using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Ai.Image.Request;
using Turbo_Auth.Models.Ai.Media.STT;

namespace Turbo_Auth.Controllers.Ai;

[ApiController]
[Authorize("vip")]
[Route("api/[controller]")]
public class MediaController : Controller
{
    private QuickModel _quickModel;

    public MediaController(
        QuickModel quickModel
    )
    {
        _quickModel = quickModel;
    }

    [HttpPost("tts")]
    public async Task<IActionResult> TTS(AudioCreateSpeechRequest speechRequest)
    {
        var modelKey = _quickModel.GetModelAndKey(speechRequest.Model);
        var openaiService = new OpenAIService(new OpenAiOptions()
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
        var openaiService = new OpenAIService(new OpenAiOptions()
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
        var openaiService = new OpenAIService(new OpenAiOptions()
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

    [HttpPost("dall-e-3")]
    public async Task<IActionResult> Dall_E_3(DallE3Request imageCreate)
    {
        var modelKey = _quickModel.GetModelAndKey(imageCreate.Model!);
        var openaiService = new OpenAIService(new OpenAiOptions()
        {
            ApiKey = modelKey!.SupplierKey!.ApiKey!,
            BaseDomain = modelKey.SupplierKey.BaseUrl!
        });
        var imageResult = await openaiService
            .Image.CreateImage(new ImageCreateRequest()
        {
            Model = imageCreate.Model,
            N = imageCreate.N,
            Prompt = imageCreate.Prompt!,
            Quality = imageCreate.Quality,
            ResponseFormat = imageCreate.ResponseFormat,
            Size = imageCreate.Size,
            Style = imageCreate.Style
        });
        // Console.WriteLine(imageResult);
        return Ok(imageResult);
    }
}