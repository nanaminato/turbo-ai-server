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
    [FeatureAccess("image"), HttpPost("dall-e")]
    public async Task<ActionResult<ImageGenerationResponse>> Dall_E(ImageGenerationRequest request, CancellationToken token) => Ok(await media.Image(request, true, token));
    [FeatureAccess("image"), HttpPost("gpt-image")]
    public async Task<ActionResult<ImageGenerationResponse>> Gpt_image(ImageGenerationRequest request, CancellationToken token) => Ok(await media.Image(request, false, token));
}
