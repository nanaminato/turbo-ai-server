namespace Turbo_Kit.PDF;

public interface IPdfDocumentProcessor//: IDocumentProcessor
{
    abstract string Process(string localPath);
}