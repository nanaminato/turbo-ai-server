using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Application.Media;
using Turbo.Auth.Contracts.Media;
using Turbo.Auth.Security.FeatureAccess;

namespace Turbo.Auth.Controllers.AI.OpenAI;

[ApiController, Authorize, Route("api/[controller]")]
public sealed class MediaController(IMediaService media) : ControllerBase
{
    [FeatureAccess("tts"), HttpPost("tts")]
    public async Task<ActionResult<SpeechResponse>> TTS(SpeechRequest request, CancellationToken token) => Ok(await media.Speech(request, token));
    [FeatureAccess("translation"), HttpPost("whisper-translate")]
    public async Task<ActionResult<AudioTextResponse>> WhisperTranslate(AudioTranslationRequest request, CancellationToken token) => Ok(await media.Translate(request, token));
    [FeatureAccess("transcription"), HttpPost("whisper-transcription")]
    public async Task<ActionResult<AudioTextResponse>> WhisperTranscription(AudioTranscriptionRequest request, CancellationToken token) => Ok(await media.Transcribe(request, token));
    [HttpPost("dall-e"), ProducesResponseType(StatusCodes.Status410Gone)]
    public ActionResult<ImageGenerationResponse> Dall_E() => StatusCode(410, new ProblemDetails { Status = 410, Title = "This image model is no longer supported", Extensions = { ["code"] = "image_model_removed" } });
    [FeatureAccess("image"), HttpPost("gpt-image")]
    public async Task<ActionResult<ImageGenerationResponse>> Gpt_image(ImageGenerationRequest request, CancellationToken token) => await Generate(request, false, token);
    private async Task<ActionResult<ImageGenerationResponse>> Generate(ImageGenerationRequest request, bool dalle, CancellationToken token)
    {
        try { return Ok(await media.Image(request, dalle, token)); }
        catch (ImageRequestException error)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid image generation option",
                Extensions = { ["code"] = "invalid_image_option", ["field"] = error.Field } });
        }
        catch (Exception error) when (error is System.Text.Json.JsonException or InvalidDataException)
        {
            return StatusCode(502, new ProblemDetails { Status = 502, Title = "Invalid image provider response",
                Extensions = { ["code"] = "image_provider_error" } });
        }
        catch (System.ClientModel.ClientResultException error)
        {
            var status = error.Status == 429 ? 429 : error.Status == 400 ? 400 : 502;
            var code = status == 429 ? "image_provider_busy" : status == 400 ? "image_provider_rejected" : "image_provider_error";
            return StatusCode(status, new ProblemDetails { Status = status, Title = "Image generation could not be completed",
                Extensions = { ["code"] = code } });
        }
    }
}
