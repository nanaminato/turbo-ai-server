namespace Turbo.Kit.Text;

public interface ITextDocumentProcessor//: IDocumentProcessor
{
    abstract string Process(string localPath);
}