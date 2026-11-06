using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RelaxKon.Auth.Application.Media;
using RelaxKon.Auth.Contracts.Common;
using RelaxKon.Auth.Contracts.Media;
using RelaxKon.Auth.Security.FeatureAccess;
namespace RelaxKon.Auth.Controllers.AI.APIMart;
[FeatureAccess("image-task"),ApiController,Authorize,Route("api/apimart")]
public class APIMartGPTTaskController(APIMartImageService images):ControllerBase
{
    [HttpGet("getTask/{task_id}")]
    public async Task<ActionResult<TaskResponse>> GPTImage2OfficialGenerate(string task_id,string? language,CancellationToken token=default)
    {
        var account=FeatureIdentity.AccountId(User);if(account is null)return Unauthorized();
        try
        {
            var result=await images.GetTask(account.Value,task_id,language,token);
            return result is null?NotFound():Ok(result);
        }
        catch(APIMartProviderException error){return StatusCode(error.Status,new CodeResponse(error.Code));}
        catch(HttpRequestException){return StatusCode(502,new CodeResponse("image_provider_error"));}
        catch(OperationCanceledException)when(!token.IsCancellationRequested){return StatusCode(504,new CodeResponse("image_provider_busy"));}
    }
}
