using System.Text;

namespace Turbo_Kit.Text;

public class TextDocumentProcessor: ITextDocumentProcessor
{
    public string Process(string localPath)
    {
        using var sr = new StreamReader(localPath);
        var builder = new StringBuilder();
        while (sr.ReadLine() is { } line)
        {
            builder.AppendLine(line);
        }

        return builder.ToString();
    }
    
}