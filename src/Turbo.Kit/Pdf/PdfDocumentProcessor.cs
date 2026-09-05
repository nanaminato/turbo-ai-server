using System.Text;
using UglyToad.PdfPig;

namespace Turbo.Kit.Pdf;

public class PdfDocumentProcessor: IPdfDocumentProcessor
{
    public string Process(string localPath)
    {
        using var document = PdfDocument.Open(localPath);
        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            builder.Append(page.Text);
        }

        return builder.ToString();
    }
}
