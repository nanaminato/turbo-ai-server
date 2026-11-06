using RelaxKon.Auth.Contracts.Files;
using RelaxKon.Kit.Pdf;
using RelaxKon.Kit.Text;
using RelaxKon.Kit.Word;

namespace RelaxKon.Auth.Controllers.Files;

public sealed class FileContentExtractor(IPdfDocumentProcessor pdf, IWordDocumentProcessor word,
    ITextDocumentProcessor text) : IFileContentExtractor
{
    public string Extractor(FileExtractionRequest file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        Func<string, string> process = extension switch
        {
            ".pdf" => pdf.Process,
            ".docx" => word.Process,
            ".txt" or ".md" or ".csv" or ".tsv" or ".json" or ".log" => text.Process,
            _ when file.FileType == "text/plain" && extension != ".doc" => text.Process,
            _ => throw new NotSupportedException("Supported formats include PDF, DOCX, TXT, Markdown, CSV, TSV, JSON and log files.")
        };

        var bytes = Convert.FromBase64String(file.FileContent);
        if (bytes.Length == 0 || bytes.Length > FileExtractionRequest.MaxFileBytes)
            throw new InvalidDataException("The file must contain between 1 byte and 10 MiB.");

        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            File.WriteAllBytes(path, bytes);
            var content = process(path);
            return string.IsNullOrWhiteSpace(content)
                ? throw new InvalidDataException("The file contains no extractable text.")
                : content;
        }
        finally
        {
            File.Delete(path);
        }
    }
}
