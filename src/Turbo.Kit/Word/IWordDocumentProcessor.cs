namespace Turbo.Kit.Word;

public interface IWordDocumentProcessor//: IDocumentProcessor
{
    abstract string Process(string localPath);
}