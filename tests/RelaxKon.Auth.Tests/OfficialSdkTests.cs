using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RelaxKon.Auth.Application.Chat;
using RelaxKon.Auth.Application.Media;
using RelaxKon.Auth.Application.Providers;
using RelaxKon.Auth.Application.Routing;
using RelaxKon.Auth.Contracts.Media;
using RelaxKon.Auth.Models.Suppliers;

namespace RelaxKon.Auth.Tests;

public class OfficialSdkTests
{
    [TestCase(true, false)]
    [TestCase(true, true)]
    [TestCase(false, false)]
    public async Task Openai_usage_is_logged_without_content_or_invented_cache_write_counts(bool includeUsage, bool includeWriteCount)
    {
        var usage = includeUsage
            ? "data: {\"id\":\"test\",\"object\":\"chat.completion.chunk\",\"created\":0,\"model\":\"test\",\"choices\":[],\"usage\":{\"prompt_tokens\":1200,\"completion_tokens\":20,\"total_tokens\":1220,\"prompt_tokens_details\":{\"cached_tokens\":1024}}}\n\n"
            : "";
        if (includeWriteCount) usage = usage.Replace("\"cached_tokens\":1024", "\"cached_tokens\":1024,\"cache_write_tokens\":176");
        var transport = new Transport(HandlerType.Openai) { Reply =
            "data: {\"id\":\"test\",\"object\":\"chat.completion.chunk\",\"created\":0,\"model\":\"test\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"private answer\"},\"finish_reason\":\"stop\"}]}\n\n" + usage + "data: [DONE]\n\n" };
        var logger = new UsageLogger();
        var handler = new OpenAiChatHandler(new OpenAiClientFactory(new Factory(transport)), logger);
        var chunks = new List<string>();
        await foreach (var chunk in handler.Stream(new ChatRequest { Messages = [new Message { Content = "private question" }] }, Route("test"), default)) chunks.Add(chunk);
        Assert.That(string.Concat(chunks), Is.EqualTo("private answer"));
        Assert.That(JObject.Parse(transport.Body!)["stream_options"]?["include_usage"]?.Value<bool>(), Is.True);
        Assert.That(logger.Events.Count, Is.EqualTo(includeUsage ? 1 : 0));
        if (includeUsage)
        {
            var entry = logger.Events.Single();
            Assert.Multiple(() =>
            {
                Assert.That(entry["InputTokens"], Is.EqualTo(1200));
                Assert.That(entry["OutputTokens"], Is.EqualTo(20));
                Assert.That(entry["CachedInputTokens"], Is.EqualTo(1024));
                Assert.That(entry["CacheWriteTokens"], Is.EqualTo(includeWriteCount ? 176L : (long?)null));
                Assert.That(string.Join(" ", entry.Values), Does.Not.Contain("private question").And.Not.Contain("private answer").And.Not.Contain("test-key"));
            });
        }
    }

    [TestCase(HandlerType.Openai)]
    [TestCase(HandlerType.Alibaba)]
    [TestCase(HandlerType.Google)]
    [TestCase(HandlerType.Anthropic)]
    public async Task Official_clients_preserve_system_vision_alias_and_transport(HandlerType provider)
    {
        var transport = new Transport(provider);
        var factory = new Factory(transport);
        IChatHandler handler = provider switch
        {
            HandlerType.Google => new GoogleChatHandler(factory),
            HandlerType.Anthropic => new AnthropicChatHandler(factory),
            HandlerType.Alibaba => new AlibabaChatHandler(new OpenAiClientFactory(factory)),
            _ => new OpenAiChatHandler(new OpenAiClientFactory(factory))
        };
        var request = new ChatRequest
        {
            Model = "logical",
            Vision = true,
            Messages = [
            new Message {Role = "system", Content = "system instruction"},
            new Message {Role = "user", Content = new ChatContent(null, [new(Text: "question"), new(ImageUrl: "data:image/png;base64,AQID")])}
        ]
        };
        var chunks = new List<string>();
        await foreach (var chunk in handler.Stream(request, Route("upstream-alias"), default)) chunks.Add(chunk);
        Assert.Multiple(() =>
        {
            Assert.That(string.Concat(chunks), Is.EqualTo("hello"));
            Assert.That(transport.Uri!.Host, Is.EqualTo("provider.invalid"));
            Assert.That(transport.Body, Does.Contain("system instruction").And.Contain("AQID").And.Contain("question"));
            Assert.That(transport.Body, Does.Not.Contain("temperature").And.Not.Contain("top_p").And.Not.Contain("reasoning_effort"));
            Assert.That(transport.Body + transport.Uri, Does.Contain("upstream-alias"));
            Assert.That(transport.Calls, Is.EqualTo(1));
        });
    }

    [TestCase(HandlerType.Openai)]
    [TestCase(HandlerType.Alibaba)]
    [TestCase(HandlerType.Google)]
    [TestCase(HandlerType.Anthropic)]
    public void Official_clients_leave_retries_to_router(HandlerType provider)
    {
        var transport = new Transport(provider) { Status = HttpStatusCode.TooManyRequests };
        var factory = new Factory(transport);
        IChatHandler handler = provider switch
        {
            HandlerType.Google => new GoogleChatHandler(factory),
            HandlerType.Anthropic => new AnthropicChatHandler(factory),
            HandlerType.Alibaba => new AlibabaChatHandler(new OpenAiClientFactory(factory)),
            _ => new OpenAiChatHandler(new OpenAiClientFactory(factory))
        };
        Assert.That(async () => { await foreach (var _ in handler.Stream(new ChatRequest { Messages = [new Message { Content = "hello" }] }, Route("test"), default)) { } }, Throws.Exception);
        Assert.That(transport.Calls, Is.EqualTo(1));
    }

    [TestCase("https://dashscope.aliyuncs.com/api/v1", "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions")]
    [TestCase("https://dashscope-intl.aliyuncs.com", "https://dashscope-intl.aliyuncs.com/compatible-mode/v1/chat/completions")]
    [TestCase("https://provider.invalid/workspace/v1", "https://provider.invalid/workspace/v1/chat/completions")]
    public async Task Alibaba_preserves_region_and_custom_workspace(string endpoint, string expected)
    {
        var transport = new Transport(HandlerType.Alibaba);
        var handler = new AlibabaChatHandler(new OpenAiClientFactory(new Factory(transport)));
        var route = Route("qwen-test"); route.SupplierKey!.BaseUrl = endpoint;
        await foreach (var _ in handler.Stream(new ChatRequest { Messages = [new Message { Content = "hello" }] }, route, default)) { }
        Assert.That(transport.Uri!.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public async Task Media_maps_binary_speech_and_preserves_upstream_alias()
    {
        var transport = new Transport(HandlerType.Openai) { Reply = "audio bytes", ContentType = "audio/mpeg" };
        var result = await Media(transport).Speech(new SpeechRequest { Model = "logical", Input = "hello" }, default);
        Assert.That(Convert.FromBase64String(result.Base64), Is.EqualTo(Encoding.UTF8.GetBytes("audio bytes")));
        Assert.That(result.Type, Is.EqualTo("mp3"));
        Assert.That(JObject.Parse(transport.Body!)["model"]!.Value<string>(), Is.EqualTo("upstream-alias"));
    }

    [TestCase("json", "application/json", "{\"text\":\"hello\"}")]
    [TestCase("text", "text/plain", "hello")]
    [TestCase("srt", "text/plain", "1\n00:00:00,000 --> 00:00:01,000\nhello")]
    public async Task Media_transcription_keeps_original_text_formats(string format, string type, string reply)
    {
        var transport = new Transport(HandlerType.Openai) { Reply = reply, ContentType = type };
        var result = await Media(transport).Transcribe(new AudioTranscriptionRequest
        {
            Model = "logical",
            File = "AQID",
            Suffix = "wav",
            ResponseFormat = format
        }, default);
        Assert.That(result.Text, Is.EqualTo(format == "json" ? "hello" : reply));
        Assert.That(transport.Body, Does.Contain("upstream-alias").And.Contain("audio.wav"));
    }

    [Test]
    public async Task Media_images_map_vendor_wire_names_and_bytes()
    {
        var transport = new Transport(HandlerType.Openai)
        {
            ContentType = "application/json",
            Reply =
            "{\"created\":123,\"data\":[{\"b64_json\":\"AQID\",\"revised_prompt\":\"revised\"}],\"usage\":{\"input_tokens\":1,\"output_tokens\":2,\"total_tokens\":3}}"
        };
        var result = await Media(transport, "gpt-image-2.6-flare").Image(new ImageGenerationRequest
        {
            Model = "logical",
            Prompt = "image",
            Size = "auto",
            Background = "transparent",
            OutputFormat = "png"
        }, false, default);
        var body = JObject.Parse(transport.Body!);
        Assert.Multiple(() =>
        {
            Assert.That(result.Created, Is.EqualTo(123));
            Assert.That(result.Data.Single().Base64, Is.EqualTo("AQID"));
            Assert.That(result.Data.Single().RevisedPrompt, Is.EqualTo("revised"));
            Assert.That(result.Usage!.TotalTokens, Is.EqualTo(3));
            Assert.That(body["model"]!.Value<string>(), Is.EqualTo("gpt-image-2.6-flare"));
            Assert.That(body["response_format"], Is.Null);
            Assert.That(body["background"]!.Value<string>(), Is.EqualTo("transparent"));
        });
    }

    private static MediaService Media(Transport transport, string alias = "upstream-alias")
    {
        var routes = new QuickModel();
        routes.Transfer(new QuickModel
        {
            Quick = new()
            {
                ["logical"] = [new WeightKey {RouteId = 1, Weight = 1,
            ProviderModelValue = alias, SupplierKey = Route(alias).SupplierKey}]
            }
        });
        return new MediaService(routes, new OpenAiClientFactory(new Factory(transport)));
    }
    private static ModelKey Route(string alias) => new() { Model = alias, SupplierKey = new SupplierKey { ApiKey = "test-key", BaseUrl = "https://provider.invalid" } };
    private sealed class UsageLogger : ILogger<OpenAiChatHandler>
    {
        public List<Dictionary<string, object?>> Events { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Events.Add(((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(pair => pair.Key, pair => pair.Value));
    }
    private sealed class Factory(Transport transport) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) { Assert.That(name, Is.EqualTo("AiProvider")); return new HttpClient(transport, disposeHandler: false); }
    }
    private sealed class Transport(HandlerType provider) : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }
        public string? Body { get; private set; }
        public int Calls { get; private set; }
        public string? Reply { get; init; }
        public string ContentType { get; init; } = "text/event-stream";
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Calls++; Uri = request.RequestUri;
            Body = await request.Content!.ReadAsStringAsync(token);
            var text = Reply ?? provider switch
            {
                HandlerType.Google => "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"hello\"}]},\"finishReason\":\"STOP\"}]}\n\n",
                HandlerType.Anthropic => "event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"hello\"}}\n\nevent: message_stop\ndata: {\"type\":\"message_stop\"}\n\n",
                _ => "data: {\"id\":\"test\",\"object\":\"chat.completion.chunk\",\"created\":0,\"model\":\"test\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"hello\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n"
            };
            if (Status != HttpStatusCode.OK) text = "{\"error\":{\"message\":\"rate limited\",\"type\":\"rate_limit_error\"}}";
            return new HttpResponseMessage(Status) { Content = new StringContent(text, Encoding.UTF8, ContentType) };
        }
    }
}
