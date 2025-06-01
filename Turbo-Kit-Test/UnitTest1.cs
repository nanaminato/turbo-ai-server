using Turbo_Kit.PDF;
using Turbo_Kit.Text;
using Turbo_Kit.WORD;

namespace Turbo_Kit_Test;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        var path = "C:\\Users\\betha\\RiderProjects\\Turbo-Auth\\Turbo-Kit-Test\\Resources\\04-2024届论文文档日期规定.docx";
        var res = (new WordDocumentProcessor()).Process(path);
        Console.WriteLine("word file");
        Console.WriteLine(res);
        Assert.Pass();
    }
    [Test]
    public void Test2()
    {
        var path = "C:\\Users\\betha\\RiderProjects\\Turbo-Auth\\Turbo-Kit-Test\\Resources\\04-2024届论文文档日期规定.pdf";
        var res = (new PdfDocumentProcessor()).Process(path);
        Console.WriteLine("pdf file");
        Console.WriteLine(res);
        Assert.Pass();
    }

    [Test]
    public void Test3()
    {
        var path = "C:\\Users\\betha\\Desktop\\sandbox.txt";
        Console.WriteLine((new TextDocumentProcessor()).Process(path));
    }

    [Test]
    public void Test4()
    {
        string url = "https://api.chatanywhere.com.cn:10111/sdfs/fsdfsd";
        // url = "https://api.chatanywhere.com.cn";
        Uri uri = new Uri(url);
        string path = uri.AbsolutePath;
        Console.WriteLine(uri.GetLeftPart(UriPartial.Authority));
        Console.WriteLine(path); // Output: /sdfs/fsdfsd
        string subRoute = path.TrimStart('/');

        Console.WriteLine(subRoute); // Output: sdfs/fsdfsd
        
        string[] segments = uri.Segments;

        string subRoute2 = string.Join("/", segments.Skip(1));

        Console.WriteLine(subRoute2); // Output: sdfs/fsdfsd


    }
}