using Turbo.Auth.Contracts.Files;

namespace Turbo.Auth.Controllers.Files;

public interface IFileContentExtractor
{
    string Extractor(FileExtractionRequest fileAdds);
}