using RelaxKon.Auth.Contracts.Files;

namespace RelaxKon.Auth.Controllers.Files;

public interface IFileContentExtractor
{
    string Extractor(FileExtractionRequest fileAdds);
}