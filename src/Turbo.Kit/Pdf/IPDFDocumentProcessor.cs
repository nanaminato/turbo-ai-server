namespace Turbo.Kit.Pdf;

public interface IPdfDocumentProcessor//: IDocumentProcessor
{
    abstract string Process(string localPath);
}