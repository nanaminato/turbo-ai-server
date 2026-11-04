using Turbo.Auth.Contracts.Files;
using Turbo.Kit.Pdf;
using Turbo.Kit.Text;
using Turbo.Kit.Word;
namespace Turbo.Auth.Controllers.Files;

public sealed class FileContentExtractor(IPdfDocumentProcessor pdf, IWordDocumentProcessor word,
    ITextDocumentProcessor text) : IFileContentExtractor
{
    public string Extractor(FileExtractionRequest file)
    {
        var bytes = Convert.FromBase64String(file.FileContent);
        var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
        var path = Path.Combine(Path.GetTempPath(), "turbo-extract-" + Guid.NewGuid().ToString("N") + extension);
        try
        {
            File.WriteAllBytes(path, bytes);
            if (file.FileType == "application/msword" || extension is ".doc" or ".docx") return word.Process(path);
            if (file.FileType == "application/pdf" || extension == ".pdf") return pdf.Process(path);
            if (file.FileType == "text/plain" || bytes.Length < 5 * 1024) return text.Process(path);
            throw new NotSupportedException();
        }
        finally { File.Delete(path); }
    }
}
