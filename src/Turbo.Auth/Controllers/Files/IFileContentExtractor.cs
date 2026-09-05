using Turbo.Auth.Models.Sync.Messages;

namespace Turbo.Auth.Controllers.Files;

public interface IFileContentExtractor
{
    string Extractor(FileAdds fileAdds);
}