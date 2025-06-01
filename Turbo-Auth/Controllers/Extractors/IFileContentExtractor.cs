using Turbo_Auth.Models.ClientSyncs.Messages;

namespace Turbo_Auth.Controllers.Extractors;

public interface IFileContentExtractor
{
    string Extractor(FileAdds fileAdds);
}