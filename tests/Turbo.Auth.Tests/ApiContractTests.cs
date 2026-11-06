using Microsoft.AspNetCore.RateLimiting;
using Turbo.Auth.Security.Registration;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Turbo.Auth.Application.Chat;
using Turbo.Auth.Application.Media;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Application.Sync;
using Turbo.Auth.Contracts.Files;
using Turbo.Auth.Contracts.Media;
using Turbo.Auth.Controllers.AI.Chat;
using Turbo.Auth.Controllers.Files;
using Turbo.Auth.Models.Suppliers;
using Turbo.Auth.Repositories.Catalog;
using Turbo.Auth.Security.FeatureAccess;

namespace Turbo.Auth.Tests;

[NonParallelizable]
public class ApiContractTests
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly RegistrationStub _registration = new();
    private readonly ChatStub _chat = new();
    private readonly KeysStub _keys = new();

    [OneTimeSetUp]
    public async Task Start()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(ChatController).Assembly).AddNewtonsoftJson();
        Turbo.Auth.Configuration.ApiDocumentation.AddApiDocumentation(builder.Services);
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("admin", policy => policy.RequireRole("admin"));
            options.AddPolicy("user", policy => policy.RequireRole("user"));
        });
        builder.Services.AddRateLimiter(options =>
        {
            foreach (var name in new[] { "registration", "registration-email" }) options.AddFixedWindowLimiter(name, limiter =>
            {
                limiter.PermitLimit = 1000; limiter.Window = TimeSpan.FromMinutes(1);
            });
        });
        builder.Services.AddSingleton<IRegistrationService>(_registration);
        builder.Services.AddSingleton<IChatService>(_chat);
        builder.Services.AddSingleton<IKeyRepository>(_keys);
        builder.Services.AddSingleton<IModelRepository>(new ModelRepository(null!));
        builder.Services.AddSingleton<QuickModel>();
        builder.Services.AddSingleton<IMediaService, MediaStub>();
        builder.Services.AddSingleton<IFileContentExtractor, FileStub>();
        builder.Services.AddSingleton(new DataSyncService(null!));
        builder.Services.AddSingleton(new Turbo.Auth.Security.FeatureAccess.FeatureAccessStore(builder.Configuration));
        builder.Services.AddSingleton(new Turbo.Auth.Security.FeatureAccess.FeatureIdentity(null!));
        builder.Services.AddSingleton<IFeatureUsageReader, UsageStub>();
        builder.Services.AddSingleton(new Turbo.Auth.Data.Contexts.AuthContext(builder.Configuration));
        _app = builder.Build();
        _app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Role, "user"), new Claim(ClaimTypes.Role, "admin")], "test"));
            await next();
        });
        _app.UseRateLimiter();
        _app.UseAuthorization();
        _app.MapControllers();
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    }
    [OneTimeTearDown]
    public async Task Stop() { _client.Dispose(); await _app.DisposeAsync(); }

    [TestCase("{\"model\":\"test\",\"messages\":[]}")]
    [TestCase("{\"model\":\"test\",\"messages\":[null]}")]
    [TestCase("{\"model\":\"test\",\"messages\":[{\"role\":\"tool\",\"content\":\"text\"}]}")]
    [TestCase("{\"model\":\"test\",\"messages\":[{\"content\":42}]}")]
    [TestCase("{\"model\":\"test\",\"messages\":[{\"content\":[42]}]}")]
    [TestCase("{\"model\":\"test\",\"messages\":[{\"content\":[{\"type\":{}}]}]}")]
    [TestCase("{\"model\":\"test\",\"messages\":[{\"content\":[{\"type\":\"image_url\",\"image_url\":42}]}]}")]
    [TestCase("{\"model\":\"test\",\"messages\":[{\"content\":[{\"type\":\"image_url\",\"image_url\":{\"url\":\"file:///etc/passwd\"}}]}]}")]
    public async Task Invalid_chat_content_returns_validation_problem_without_provider_call(string json)
    {
        var calls = _chat.Calls;
        using var response = await Post("api/ai/chat", json);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(JObject.Parse(await response.Content.ReadAsStringAsync())["errors"], Is.Not.Null);
        Assert.That(_chat.Calls, Is.EqualTo(calls));
    }

    [Test]
    public async Task Legacy_raw_stream_can_write_multiple_chunks()
    {
        using var response = await Post("api/ai/chat", "{\"model\":\"test\",\"messages\":[{\"content\":\"hello\"}]}");
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("replysecond"));
    }

    [Test]
    public async Task Legacy_chat_wire_shape_and_stream_remain_compatible()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/ai/chat") { Content = Json("{\"model\":\"test\",\"vision\":true,\"messages\":[{\"role\":\"user\",\"content\":[{\"type\":\"text\",\"text\":\"hello\"},{\"type\":\"image_url\",\"image_url\":{\"url\":\"https://image.invalid/a.png\"}}]}]}") };
        request.Headers.Accept.ParseAdd("text/event-stream");
        using var response = await _client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("\"type\":\"completed\""));
        Assert.That(_chat.Last!.Messages[0].Content.Parts, Has.Count.EqualTo(2));
    }

    [TestCase("api/media/tts", "{\"input\":\"hello\",\"speed\":9}")]
    [TestCase("api/media/whisper-transcription", "{\"model\":\"test\",\"file\":\"not base64!\"}")]
    [TestCase("api/FileExtractor", "{\"fileName\":\"a.txt\",\"fileContent\":\"bad!\"}")]
    [TestCase("api/sync/data", "{\"operationId\":\"00000000-0000-0000-0000-000000000001\",\"kind\":\"account\",\"action\":\"upsert\",\"entityId\":\"1\"}")]
    public async Task Invalid_requests_are_rejected_at_http_boundary(string path, string body)
    {
        using var response = await Post(path, body);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("{\"revision\":0,\"features\":null}")]
    [TestCase("{\"revision\":0,\"features\":[null]}")]
    [TestCase("{\"revision\":0,\"features\":[{\"key\":\"chat\",\"roles\":{\"user\":null}}]}")]
    [TestCase("{\"revision\":0,\"features\":[{\"key\":\"chat\",\"roles\":{\"user\":{\"requestsPerMinute\":-1}}}]}")]
    public async Task Policy_contracts_validate_nested_records_before_database_access(string json)
    {
        using var response = await _client.PutAsync("api/feature-access/policies", Json(json));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Key_update_rejects_id_mismatch_and_ignores_nested_database_graphs()
    {
        const string json = "{\"supplierKeyId\":8,\"apiKey\":\"test\",\"baseUrl\":\"https://provider.invalid/v1\",\"requestIdentifier\":0,\"modelKeyBinds\":[{\"modelId\":2,\"modelKeyBindId\":999,\"supplierKeyId\":8,\"model\":{\"modelId\":3,\"name\":\"injected\"}}]}";
        using var rejected = await _client.PutAsync("api/key/7", Json(json));
        Assert.That(rejected.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var accepted = await _client.PutAsync("api/key/8", Json(json));
        Assert.That(accepted.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_keys.Last!.SupplierKeyId, Is.EqualTo(8));
        Assert.That(_keys.Last.ModelKeyBinds!.Single().ModelKeyBindId, Is.Zero);
        Assert.That(_keys.Last.ModelKeyBinds!.Single().Model, Is.Null);
    }

    [Test]
    public void Controller_parameters_no_longer_bind_database_entities_or_vendor_models()
    {
        var methods = typeof(ChatController).Assembly.GetTypes().Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods()).Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any());
        foreach (var method in methods)
            foreach (var parameter in method.GetParameters())
                Assert.That(parameter.ParameterType.Namespace, Does.Not.StartWith("Turbo.Auth.Models").And.Not.StartWith("OpenAI").And.Not.StartWith("Anthropic").And.Not.StartWith("Google.GenAI"), method.DeclaringType!.Name + "." + method.Name);
    }

    [Test]
    public void Openapi_schema_matches_polymorphic_chat_and_snake_case_media_fields()
    {
        var swagger = _app.Services.GetRequiredService<Swashbuckle.AspNetCore.Swagger.ISwaggerProvider>().GetSwagger("v1");
        var schemas = swagger.Components!.Schemas!;
        Assert.That(schemas["ChatMessageRequest"].Properties!["content"].OneOf, Has.Count.EqualTo(2));
        Assert.That(schemas["ImageGenerationRequest"].Properties!.Keys, Does.Contain("output_format").And.Contain("complexity").And.Not.Contain("response_format").And.Not.Contain("style"));
        Assert.That(schemas["ImageResultResponse"].Properties!.Keys, Does.Contain("b64_json").And.Contain("revised_prompt"));
        Assert.That(swagger.Info.Title, Is.EqualTo("RelaxKon Server API"));
        Assert.That(swagger.Paths.Keys, Does.Contain("/api/feature-access/usage"));
        Assert.That(typeof(ChatController).Assembly.GetName().Name, Is.EqualTo("Turbo.Auth"));
    }

    [Test]
    public async Task Usage_endpoint_preserves_camel_case_nullable_limits_and_current_account_scope()
    {
        using var response = await _client.GetAsync("api/feature-access/usage?accountId=99");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.CacheControl!.NoStore, Is.True);
        var body = JObject.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body["policyRevision"]!.Value<long>(), Is.EqualTo(4));
        Assert.That(body["observedAt"]!.Type, Is.EqualTo(Newtonsoft.Json.Linq.JTokenType.Date));
        var chat = body["features"]![0]!;
        Assert.That(chat["key"]!.Value<string>(), Is.EqualTo("chat"));
        Assert.That(chat["minuteUsed"]!.Value<long>(), Is.EqualTo(1));
        Assert.That(chat["minuteRemaining"]!.Value<long>(), Is.EqualTo(9));
        Assert.That(chat["dayRemaining"]!.Type, Is.EqualTo(JTokenType.Null));
        Assert.That(chat["concurrentRemaining"]!.Value<long>(), Is.EqualTo(1));
    }

    private sealed class UsageStub : IFeatureUsageReader
    {
        public Task<FeatureUsageSnapshot> ReadUsage(int accountId, CancellationToken token)
        {
            Assert.That(accountId, Is.EqualTo(1));
            const long now = 1793923805;
            var document = FeaturePolicies.Empty with { Revision = 4 };
            document.Features[0].Roles["user"] = new(true, 10, null, 2);
            return Task.FromResult(FeatureUsageProjection.Create(now, document, ["user"],
                [new("chat", now / 60, now / 86400, 1, 5, 1)]));
        }
    }

    [TestCase("{\"username\":\"new\",\"email\":\"new@example.invalid\",\"password\":\"secret123\",\"confirm\":\"secret123\"}")]
    [TestCase("{\"username\":\"new\",\"email\":\"new@example.invalid\",\"password\":\"secret123\",\"confirm\":\"secret123\",\"emailCode\":\"abc123\"}")]
    public async Task Registration_cannot_bypass_email_code_validation(string json)
    {
        var before = _registration.RegisterCalls;
        using var response = await Post("api/auth/register", json);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(_registration.RegisterCalls, Is.EqualTo(before));
    }
    [Test]
    public async Task Email_code_response_never_returns_the_code_and_reports_server_cooldown()
    {
        _registration.Result = new(EmailCodeStatus.Sent);
        using (var response = await Post("api/auth/register/email-code", "{\"email\":\"new@example.invalid\"}"))
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
            var body = JObject.Parse(await response.Content.ReadAsStringAsync());
            Assert.That(body["code"]!.Value<string>(), Is.EqualTo("registration_code_sent"));
            Assert.That(body.Properties().Select(property => property.Name), Is.EquivalentTo(new[] { "code", "message", "expiresInSeconds", "resendAfterSeconds" }));
        }
        _registration.Result = new(EmailCodeStatus.Cooldown, 36);
        using (var response = await Post("api/auth/register/email-code", "{\"email\":\"new@example.invalid\"}"))
        {
            Assert.That((int)response.StatusCode, Is.EqualTo(429));
            Assert.That(response.Headers.RetryAfter!.Delta!.Value.TotalSeconds, Is.EqualTo(36));
        }
        _registration.Result = new(EmailCodeStatus.Sent);
    }
    private sealed class RegistrationStub : IRegistrationService
    {
        public int RegisterCalls; public EmailCodeResult Result = new(EmailCodeStatus.Sent);
        public Task<EmailCodeResult> SendCode(string email, CancellationToken token) => Task.FromResult(Result);
        public Task<RegistrationStatus> Register(Turbo.Auth.Contracts.Auth.RegisterRequest request, CancellationToken token) { RegisterCalls++; return Task.FromResult(RegistrationStatus.Success); }
    }

    private Task<HttpResponseMessage> Post(string path, string body) => _client.PostAsync(path, Json(body));
    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
    private sealed class ChatStub : IChatService
    {
        public int Calls { get; private set; }
        public ChatRequest? Last { get; private set; }
        public async IAsyncEnumerable<string> Stream(ChatRequest request, [EnumeratorCancellation] CancellationToken token)
        { Calls++; Last = request; await Task.Yield(); yield return "reply"; yield return "second"; }
    }
    private sealed class MediaStub : IMediaService
    {
        public Task<SpeechResponse> Speech(SpeechRequest request, CancellationToken token) => throw new AssertionException("Invalid request reached service");
        public Task<AudioTextResponse> Transcribe(AudioTranscriptionRequest request, CancellationToken token) => throw new AssertionException("Invalid request reached service");
        public Task<AudioTextResponse> Translate(AudioTranslationRequest request, CancellationToken token) => throw new AssertionException("Invalid request reached service");
        public Task<ImageGenerationResponse> Image(ImageGenerationRequest request, bool dalle, CancellationToken token) => throw new AssertionException("Invalid request reached service");
    }
    private sealed class FileStub : IFileContentExtractor
    { public string Extractor(FileExtractionRequest fileAdds) => throw new AssertionException("Invalid request reached service"); }
    private sealed class KeysStub : IKeyRepository
    {
        public SupplierKey? Last { get; private set; }
        public Task<List<SupplierKey>?> GetKeysAsync() => Task.FromResult<List<SupplierKey>?>([]);
        public Task<List<SupplierKey>?> GetKeysWithModelAsync(int modelId) => GetKeysAsync();
        public Task<SupplierKey?> GetKeyByIdAsync(int id) => Task.FromResult<SupplierKey?>(null);
        public Task DeleteKeyByIdAsync(int id) => Task.CompletedTask;
        public Task AddKeyAsync(SupplierKey key) { Last = key; return Task.CompletedTask; }
        public Task UpdateKeyAsync(SupplierKey key) { Last = key; return Task.CompletedTask; }
    }
}
