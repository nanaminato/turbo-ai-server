using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Turbo.Kit.Word;

public class WordDocumentProcessor: IWordDocumentProcessor
{
    private static readonly XNamespace Wordprocessing = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public string Process(string localPath)
    {
        var builder = new StringBuilder();
        using var archive = ZipFile.OpenRead(localPath);
        var documentEntry = archive.GetEntry("word/document.xml")
            ?? throw new InvalidDataException("The DOCX file does not contain word/document.xml.");
        using var documentStream = documentEntry.Open();
        var document = XDocument.Load(documentStream);

        foreach (var paragraph in document.Descendants(Wordprocessing + "p"))
        {
            foreach (var node in paragraph.Descendants())
            {
                if (node.Name == Wordprocessing + "t")
                {
                    builder.Append(node.Value);
                }
                else if (node.Name == Wordprocessing + "tab")
                {
                    builder.Append('\t');
                }
            }

            if (paragraph.Descendants(Wordprocessing + "t").Any())
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}
