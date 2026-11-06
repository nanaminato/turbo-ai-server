using RelaxKon.Auth.Application.Media;
using RelaxKon.Auth.Security.FeatureAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RelaxKon.Auth.Contracts.Common;
using RelaxKon.Auth.Contracts.Media;

namespace RelaxKon.Auth.Controllers.AI.APIMart;

// Keep retired creation URLs explicit; task-result retrieval lives in APIMartTaskController.
[ApiController, Authorize, Route("api/apimart/image-generate")]
public class APIMartGPTImageController(APIMartImageService? images=null) : ControllerBase
{
    [HttpPost("gpt-image-2"), ProducesResponseType(StatusCodes.Status410Gone)]
    public ActionResult<APIMartGenerationResponse> GPTImage2Generate() => StatusCode(410, new CodeResponse("removed_image_model"));
    [HttpPost("gpt-image-2-official"), ProducesResponseType(StatusCodes.Status410Gone)]
    public ActionResult<APIMartGenerationResponse> GPTImage2OfficialGenerate() => StatusCode(410, new CodeResponse("removed_image_model"));
    [FeatureAccess("image"),HttpPost("gpt-image"),RequestSizeLimit(32*1024*1024)]
    public async Task<ActionResult<APIMartGenerationResponse>> Generate(APIMartImageGenerationRequest request,CancellationToken token)
    {
        var account=FeatureIdentity.AccountId(User);if(account is null)return Unauthorized();
        try{return Ok(await images!.Generate(account.Value,request,token));}
        catch(ImageRequestException error){return BadRequest(new {code="invalid_image_option",field=error.Field});}
        catch(APIMartReceiptPendingException error){return ReceiptPending(error);}
        catch(APIMartProviderException error){return StatusCode(error.Status,new CodeResponse(error.Code));}
        catch(HttpRequestException){return StatusCode(502,new CodeResponse("image_submission_unknown"));}
        catch(OperationCanceledException)when(!token.IsCancellationRequested){return StatusCode(504,new CodeResponse("image_submission_unknown"));}
    }
    [HttpPost("receipt")]
    public async Task<ActionResult<APIMartGenerationResponse>> Recover(APIMartReceiptRecoveryRequest request)
    {
        var account=FeatureIdentity.AccountId(User);if(account is null)return Unauthorized();
        try{return Ok(await images!.Recover(account.Value,request.RecoveryToken));}
        catch(ImageRequestException error){return BadRequest(new {code="invalid_image_option",field=error.Field});}
        catch(APIMartReceiptPendingException error){return ReceiptPending(error);}
    }
    private ObjectResult ReceiptPending(APIMartReceiptPendingException error)=>StatusCode(502,new {code="image_task_receipt_pending",task_ids=error.Response.Data!.Select(task=>task.TaskId).ToArray(),recovery_token=error.RecoveryToken});
}
