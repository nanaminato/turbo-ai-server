using Turbo.Auth.Security.FeatureAccess;
using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Contracts.Files;

namespace Turbo.Auth.Controllers.Files;

[Microsoft.AspNetCore.Authorization.Authorize]
[FeatureAccess("file-extraction")]
[ApiController]
[Route("api/[controller]")]
public class FileExtractorController : Controller
{
    private IFileContentExtractor _fileContentExtractor;
    public FileExtractorController(IFileContentExtractor fileContentExtractor)
    {
        _fileContentExtractor = fileContentExtractor;

    }
    [HttpPost]
    public ActionResult<FileExtractionResponse> Parse(FileExtractionRequest file)
    {
        var res = _fileContentExtractor.Extractor(file);
        return Ok(new FileExtractionResponse(res));
    }
}