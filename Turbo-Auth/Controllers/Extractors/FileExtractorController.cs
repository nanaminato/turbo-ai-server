using Microsoft.AspNetCore.Mvc;
using Turbo_Auth.Models.ClientSyncs.Messages;

namespace Turbo_Auth.Controllers.Extractors;
[ApiController]
[Route("api/[controller]")]
public class FileExtractorController: Controller
{
    private IFileContentExtractor _fileContentExtractor;
    public FileExtractorController(IFileContentExtractor fileContentExtractor)
    {
        _fileContentExtractor = fileContentExtractor;
        
    }
    [HttpPost]
    public IActionResult Parse(FileAdds file)
    {
        var res = _fileContentExtractor.Extractor(file);
        return Ok(new
        {
            Content = res
        });
    }
}