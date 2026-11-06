using RelaxKon.Auth.Security.FeatureAccess;
using Microsoft.AspNetCore.Mvc;
using RelaxKon.Auth.Contracts.Files;
using UglyToad.PdfPig.Core;

namespace RelaxKon.Auth.Controllers.Files;

[Microsoft.AspNetCore.Authorization.Authorize]
[FeatureAccess("file-extraction")]
[ApiController]
[Route("api/[controller]")]
public class FileExtractorController(IFileContentExtractor fileContentExtractor) : Controller
{
    [HttpPost]
    public ActionResult<FileExtractionResponse> Parse(FileExtractionRequest file)
    {
        try
        {
            return Ok(new FileExtractionResponse(fileContentExtractor.Extractor(file)));
        }
        catch (NotSupportedException)
        {
            return Problem(statusCode: 415, title: "Unsupported file format.");
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException
            or System.Xml.XmlException or PdfDocumentFormatException)
        {
            return Problem(statusCode: 400, title: "The file is invalid or contains no extractable text.");
        }
    }
}
