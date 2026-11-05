using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Contracts.Common;
using Turbo.Auth.Contracts.Media;

namespace Turbo.Auth.Controllers.AI.APIMart;

// Keep retired creation URLs explicit; task-result retrieval lives in APIMartTaskController.
[ApiController, Authorize, Route("api/apimart/image-generate")]
public class APIMartGPTImageController : ControllerBase
{
    [HttpPost("gpt-image-2"), ProducesResponseType(StatusCodes.Status410Gone)]
    public ActionResult<APIMartGenerationResponse> GPTImage2Generate() => StatusCode(410, new CodeResponse("removed_image_model"));
    [HttpPost("gpt-image-2-official"), ProducesResponseType(StatusCodes.Status410Gone)]
    public ActionResult<APIMartGenerationResponse> GPTImage2OfficialGenerate() => StatusCode(410, new CodeResponse("removed_image_model"));
}
