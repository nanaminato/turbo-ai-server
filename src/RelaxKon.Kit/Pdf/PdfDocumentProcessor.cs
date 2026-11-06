using System.Text;
using UglyToad.PdfPig;

namespace RelaxKon.Kit.Pdf;

public class PdfDocumentProcessor: IPdfDocumentProcessor
{
    public string Process(string localPath)
    {
        using var document = PdfDocument.Open(localPath);
        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return builder.ToString();
    }
}
