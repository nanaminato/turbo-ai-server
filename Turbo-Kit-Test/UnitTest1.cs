using System.IO.Compression;
using System.Text;
using Turbo_Auth.Models.Accounts;
using Turbo_Auth.Security;
using Turbo_Kit.PDF;
using Turbo_Kit.Text;
using Turbo_Kit.WORD;

namespace Turbo_Kit_Test;

public class DocumentProcessorTests
{
    private string _testDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "turbo-kit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_testDirectory, recursive: true);
    }

    [Test]
    public void TextProcessor_reads_utf8_content()
    {
        var path = Path.Combine(_testDirectory, "sample.txt");
        File.WriteAllText(path, "first line\n第二行", Encoding.UTF8);

        var result = new TextDocumentProcessor().Process(path);

        Assert.That(result, Does.Contain("first line"));
        Assert.That(result, Does.Contain("第二行"));
    }

    [Test]
    public void WordProcessor_reads_document_xml_from_a_docx_archive()
    {
        var path = Path.Combine(_testDirectory, "sample.docx");
        CreateDocx(path, "Word fixture", "表格内容");

        var result = new WordDocumentProcessor().Process(path);

        Assert.That(result, Does.Contain("Word fixture"));
        Assert.That(result, Does.Contain("表格内容"));
    }

    [Test]
    public void PdfProcessor_extracts_text_from_a_self_contained_fixture()
    {
        var path = Path.Combine(_testDirectory, "sample.pdf");
        CreatePdf(path, "PDF fixture");

        var result = new PdfDocumentProcessor().Process(path);

        Assert.That(result, Does.Contain("PDF fixture"));
    }

    private static void CreateDocx(string path, string firstLine, string secondLine)
    {
        const string wordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var documentXml = $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="{wordNamespace}">
              <w:body>
                <w:p><w:r><w:t>{firstLine}</w:t></w:r></w:p>
                <w:p><w:r><w:t>{secondLine}</w:t></w:r></w:p>
              </w:body>
            </w:document>
            """;

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("word/document.xml");
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(documentXml);
    }

    private static void CreatePdf(string path, string text)
    {
        var contentStream = $"BT\n/F1 12 Tf\n72 720 Td\n({text}) Tj\nET\n";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(contentStream)} >>\nstream\n{contentStream}endstream"
        };

        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n0 ").Append(objects.Length + 1).Append("\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            builder.Append(offset.ToString("D10")).Append(" 00000 n \n");
        }

        builder.Append("trailer\n<< /Size ").Append(objects.Length + 1)
            .Append(" /Root 1 0 R >>\nstartxref\n").Append(xrefOffset).Append("\n%%EOF\n");
        File.WriteAllText(path, builder.ToString(), Encoding.ASCII);
    }
}

public class AccountPasswordServiceTests
{
    private readonly IAccountPasswordService _service = new AccountPasswordService();

    [Test]
    public void Hash_and_verify_do_not_keep_the_plaintext_password()
    {
        var account = new Account { Username = "user" };
        account.Password = _service.Hash(account, "a secure password");

        Assert.That(account.Password, Is.Not.EqualTo("a secure password"));
        Assert.That(_service.Verify(account, "a secure password"), Is.EqualTo(PasswordVerificationState.Valid));
        Assert.That(_service.Verify(account, "incorrect"), Is.EqualTo(PasswordVerificationState.Invalid));
    }

    [Test]
    public void Legacy_plaintext_password_is_accepted_once_and_marked_for_upgrade()
    {
        var account = new Account { Username = "legacy", Password = "legacy password" };

        Assert.That(_service.Verify(account, "legacy password"), Is.EqualTo(PasswordVerificationState.ValidNeedsUpgrade));
        Assert.That(_service.Verify(account, "incorrect"), Is.EqualTo(PasswordVerificationState.Invalid));
    }
}
