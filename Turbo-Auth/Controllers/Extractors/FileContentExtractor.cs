using Turbo_Auth.Models.ClientSyncs.Messages;
using Turbo_Kit.PDF;
using Turbo_Kit.Text;
using Turbo_Kit.WORD;

namespace Turbo_Auth.Controllers.Extractors;

public class FileContentExtractor: IFileContentExtractor
{
    private IPdfDocumentProcessor _pdfDocumentProcessor;
    private IWordDocumentProcessor _wordDocumentProcessor;
    private ITextDocumentProcessor _textDocumentProcessor;
    public FileContentExtractor(IPdfDocumentProcessor pdfDocumentProcessor,
        IWordDocumentProcessor wordDocumentProcessor, ITextDocumentProcessor textDocumentProcessor)
    {
        _pdfDocumentProcessor = pdfDocumentProcessor;
        _wordDocumentProcessor = wordDocumentProcessor;
        _textDocumentProcessor = textDocumentProcessor;
    }
    public string Extractor(FileAdds fileAdds)
    {
        var fileBytes = Convert.FromBase64String(fileAdds.FileContent!);

        var loc = "resources//uploads//"+Guid.NewGuid() + fileAdds.FileName;
        var parentDirectory = Path.GetDirectoryName(loc);

        // 检查父目录是否存在，如果不存在则创建
        if (!Directory.Exists(parentDirectory))
        {
            Directory.CreateDirectory(parentDirectory!);
            Console.WriteLine("父目录已创建");
        }
        using (var fileStream = new FileStream(loc, FileMode.Create))
        {
            fileStream.Write(fileBytes, 0, fileBytes.Length);
        }
        if (fileAdds.FileType == "application/msword" || fileAdds.FileName!.EndsWith(".docx") ||
            fileAdds.FileName!.EndsWith(".doc"))
        {
            return _wordDocumentProcessor.Process(loc);
        }
        if (fileAdds.FileType == "application/pdf" || fileAdds.FileName!.EndsWith(".pdf"))
        {
            return _pdfDocumentProcessor.Process(loc);
        }
        if (fileAdds.FileType == "text/plain"||fileAdds.FileSize / 1024 < 5)
        {
            return _textDocumentProcessor.Process(loc);
        }

        throw new NotSupportedException();
    }
}