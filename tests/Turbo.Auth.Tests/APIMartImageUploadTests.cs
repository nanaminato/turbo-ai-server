using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Controllers.AI.APIMart;
using Turbo.Auth.Models.Suppliers;

namespace Turbo.Auth.Tests;

public class APIMartImageUploadTests
{
    private static readonly byte[] Png = [137,80,78,71,13,10,26,10,0,0,0,0];
    private sealed class Transport : HttpMessageHandler, IHttpClientFactory
    {
        public int Calls;
        public Uri? Address;
        public string? Authorization;
        public string? Body;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public string Reply = """{"url":"https://upload.apimart.ai/reference.png","private":"test-secret"}""";
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++; Address = request.RequestUri; Authorization = request.Headers.Authorization?.ToString();
            Body = await request.Content!.ReadAsStringAsync(token);
            return new(Status) { Content = new StringContent(Reply, Encoding.UTF8, "application/json") };
        }
    }
    private static APIMartImageUploadController Controller(Transport transport, bool key = true, string address = "https://provider.invalid/v1")
    {
        var routes = new QuickModel();
        if(key) routes.Transfer(new QuickModel { ApiMartKeys = [new SupplierKey { SupplierKeyId=7, Enable=true, RequestIdentifier=(int)Turbo.Auth.Application.Providers.HandlerType.ApiMart, ApiKey="test-secret", BaseUrl=address }] });
        return new(routes,transport);
    }
    private static IFormFile File(byte[]? bytes = null, string type = "image/png") => new FormFile(new MemoryStream(bytes ?? Png),0,(bytes ?? Png).Length,"file","C:\\private\\photo.png") { Headers = new HeaderDictionary(), ContentType = type };

    [Test] public async Task Upload_uses_multipart_file_and_provider_credentials_and_returns_only_public_url()
    {
        var transport = new Transport();
        var result = await Controller(transport).Upload(File(), default);
        var response = (APIMartImageUploadController.UploadResponse)((OkObjectResult)result.Result!).Value!;
        Assert.Multiple(() => {
            Assert.That(response.Url, Is.EqualTo("https://upload.apimart.ai/reference.png"));
            Assert.That(transport.Address!.AbsoluteUri, Is.EqualTo("https://provider.invalid/v1/uploads/images"));
            Assert.That(transport.Authorization, Is.EqualTo("Bearer test-secret"));
            Assert.That(transport.Body, Does.Contain("name=file").Or.Contain("name=\"file\""));
            Assert.That(transport.Body, Does.Not.Contain("private").And.Not.Contain("photo.png"));
        });
    }
    [TestCase("text/html")]
    [TestCase("image/svg+xml")]
    public async Task Unsupported_files_never_reach_the_provider(string type)
    {
        var transport = new Transport();
        Assert.That((await Controller(transport).Upload(File(type:type),default)).Result, Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(transport.Calls, Is.Zero);
    }
    [Test] public async Task Fake_png_and_empty_files_are_rejected_without_upload()
    {
        var transport = new Transport();
        Assert.That((await Controller(transport).Upload(File(Encoding.UTF8.GetBytes("<html>bad</html>")),default)).Result, Is.InstanceOf<BadRequestObjectResult>());
        Assert.That((await Controller(transport).Upload(File([]),default)).Result, Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(transport.Calls, Is.Zero);
    }
    [Test] public async Task Oversize_file_is_rejected_before_streaming()
    {
        var transport = new Transport();
        var file = new FormFile(new MemoryStream(Png),0,APIMartImageUploadController.MaximumBytes+1,"file","large.png"){ Headers=new HeaderDictionary(),ContentType="image/png" };
        Assert.That((await Controller(transport).Upload(file,default)).Result, Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(transport.Calls, Is.Zero);
    }
    [TestCase("not json")]
    [TestCase("{\"url\":\"javascript:alert(1)\"}")]
    [TestCase("{\"url\":\"https://user:secret@example.com/file.png\"}")]
    public async Task Invalid_provider_responses_are_sanitized(string reply)
    {
        var transport = new Transport{Reply=reply};
        var result = (ObjectResult)(await Controller(transport).Upload(File(),default)).Result!;
        Assert.That(result.StatusCode, Is.EqualTo(502));
        Assert.That(System.Text.Json.JsonSerializer.Serialize(result.Value), Does.Not.Contain("secret"));
    }
    [Test] public async Task Missing_key_and_provider_rate_limit_have_safe_errors()
    {
        var transport = new Transport{Status=HttpStatusCode.TooManyRequests,Reply="test-secret"};
        Assert.That(((ObjectResult)(await Controller(transport,false).Upload(File(),default)).Result!).StatusCode, Is.EqualTo(503));
        Assert.That(transport.Calls, Is.Zero);
        var limited=(ObjectResult)(await Controller(transport).Upload(File(),default)).Result!;
        Assert.That(limited.StatusCode, Is.EqualTo(429));
        Assert.That(System.Text.Json.JsonSerializer.Serialize(limited.Value), Does.Not.Contain("test-secret"));
    }
}
