using System.Text;
using NPOI.XWPF.UserModel;

namespace Turbo_Kit.WORD;
public class WordDocumentProcessor: IWordDocumentProcessor
{
    public string Process(string localPath)
    {
        var builder = new StringBuilder();
        using var file = new FileStream(localPath, FileMode.Open, FileAccess.Read);
        var doc = new XWPFDocument(file);

        foreach (var para in doc.Paragraphs)
        {
            builder.AppendLine(para.Text);
        }

        foreach (var table in doc.Tables)
        {
            foreach (var row in table.Rows)
            {
                foreach (var cell in row.GetTableCells())
                {
                    builder.Append(cell.GetText());
                    builder.Append('\t');
                }
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}