using System.Text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;

namespace Turbo_Kit.PDF;

public class PdfDocumentProcessor: IPdfDocumentProcessor
{
    public string Process(string localPath)
    {
        using var reader = new PdfReader(localPath);
        var builder = new StringBuilder();
        for (var page = 1; page <= reader.NumberOfPages; page++)
        {
            ITextExtractionStrategy strategy = new SimpleTextExtractionStrategy();
            var text = PdfTextExtractor.GetTextFromPage(reader, page, strategy);
            builder.Append(text);
        }

        return builder.ToString();
    }
}