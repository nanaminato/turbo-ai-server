using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RelaxKon.Auth.Application.Chat;
using RelaxKon.Auth.Application.Providers;
using RelaxKon.Auth.Application.Routing;
using RelaxKon.Auth.Controllers.AI.Chat;
using RelaxKon.Auth.Contracts.Chat;
using RelaxKon.Auth.Models.Suppliers;

namespace RelaxKon.Auth.Tests;

public class ChatStreamTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Api_emits_an_explicit_terminal_event(bool fail)
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider() };
        context.Response.Body = new MemoryStream();
        context.Request.Headers.Accept = "text/event-stream";
        var controller = new ChatController(new FakeChat(fail), null!, null!, NullLogger<ChatController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = context } };
        await controller.Chat(new ChatCompletionRequest { Model = "test" });
        context.Response.Body.Position = 0;
        var events = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.That(events, Does.Contain("\"type\":\"delta\""));
        Assert.That(events, Does.Contain(fail ? "\"type\":\"error\"" : "\"type\":\"completed\""));
        if (fail) Assert.That(events, Does.Not.Contain("\"type\":\"completed\""));
        Assert.That(context.Response.ContentType, Does.StartWith("text/event-stream"));
    }

    [Test]
    public async Task Legacy_clients_receive_text_instead_of_event_frames()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var controller = new ChatController(new FakeChat(false), null!, null!, NullLogger<ChatController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = context } };
        await controller.Chat(new ChatCompletionRequest { Model = "test" });
        context.Response.Body.Position = 0;
        Assert.That(await new StreamReader(context.Response.Body).ReadToEndAsync(), Is.EqualTo("你好\nreply"));
    }

    [TestCase(false, 2)]
    [TestCase(true, 1)]
    public async Task Only_retries_before_the_first_delta(bool partial, int calls)
    {
        var handler = new FailingHandler(partial);
        var routes = await new ModelKeyBuilder().Build(Enumerable.Range(1, 2).Select(id =>
        {
            var key = new SupplierKey { Enable = true, ApiKey = "test", RequestIdentifier = (int)HandlerType.Openai };
            key.ModelKeyBinds = [new ModelKeyBind { ModelKeyBindId = id, Enable = true, Fee = 1,
                Priority = id, SupplierKey = key, Model = new Model { ModelValue = "test" } }];
            return key;
        }).ToList());
        var active = new QuickModel();
        active.Transfer(routes);
        var service = new ChatService(new ChatHandlerObtain([handler]), active, new RouteHealthTracker(Microsoft.Extensions.Options.Options.Create(new AiRoutingOptions())),
            new PlayMixModelBacker(), NullLogger<ChatService>.Instance);
        var output = new List<string>();
        try { await foreach (var text in service.Stream(new ChatRequest { Model = "test" }, default)) output.Add(text); }
        catch (InvalidOperationException) when (partial) { }
        Assert.That(handler.Calls, Is.EqualTo(calls));
        Assert.That(output, Is.EqualTo(new[] { partial ? "partial" : "fallback" }));
    }

    [Test]
    public async Task Unsupported_primary_provider_can_fall_back_before_any_output()
    {
        var routes = new QuickModel();
        routes.Transfer(new QuickModel
        {
            Quick = new()
            {
                ["test"] = [
            new WeightKey {RouteId = 1, Priority = 0, Weight = 1, ProviderModelValue = "test",
                SupplierKey = new SupplierKey {RequestIdentifier = (int)HandlerType.Twitter}},
            new WeightKey {RouteId = 2, Priority = 1, Weight = 1, ProviderModelValue = "test",
                SupplierKey = new SupplierKey {RequestIdentifier = (int)HandlerType.Openai}}
        ]
            }
        });
        var handler = new FailingHandler(false);
        var service = new ChatService(new ChatHandlerObtain([handler]), routes,
            new RouteHealthTracker(Microsoft.Extensions.Options.Options.Create(new AiRoutingOptions())),
            new PlayMixModelBacker(), NullLogger<ChatService>.Instance);
        var output = new List<string>();
        await foreach (var text in service.Stream(new ChatRequest { Model = "test" }, default)) output.Add(text);
        Assert.That(output, Is.EqualTo(new[] { "fallback" }));
        Assert.That(handler.Calls, Is.EqualTo(1));
    }

    [Test]
    public async Task Openai_uses_the_configured_http_transport()
    {
        var transport = new StubTransport();
        var handler = new OpenAiChatHandler(new OpenAiClientFactory(new StubFactory(new HttpClient(transport))));
        await foreach (var _ in handler.Stream(new ChatRequest
        {
            Model = "logical",
            Messages = [new RelaxKon.Auth.Application.Chat.Message { Role = "user", Content = "hello" }]
        }, new ModelKey { Model = "upstream", SupplierKey = new SupplierKey { ApiKey = "test", BaseUrl = "https://test.invalid/v1" } }, default)) { }
        Assert.That(transport.RequestUri?.Host, Is.EqualTo("test.invalid"));
        Assert.That(transport.Body, Does.Contain("upstream"));
    }

    [TestCase(HandlerType.Google)]
    [TestCase(HandlerType.Anthropic)]
    [TestCase(HandlerType.Alibaba)]
    public async Task Provider_stream_requires_a_terminal_marker(HandlerType provider)
    {
        var body = provider switch
        {
            HandlerType.Google => "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"hello\"}]},\"finishReason\":\"STOP\"}]}\n\n",
            HandlerType.Anthropic => """
                event: message_start
                data: {"type":"message_start","message":{"id":"test","type":"message","role":"assistant","model":"test","content":[],"stop_reason":null,"usage":{"input_tokens":1,"output_tokens":0}}}

                event: content_block_start
                data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

                event: content_block_delta
                data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"hello"}}

                event: content_block_stop
                data: {"type":"content_block_stop","index":0}

                event: message_delta
                data: {"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":1}}

                event: message_stop
                data: {"type":"message_stop"}

                """ + "\n\n",
            _ => "data: {\"id\":\"test\",\"object\":\"chat.completion.chunk\",\"created\":0,\"model\":\"test\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"hello\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n"
        };
        var factory = new StubFactory(new HttpClient(new StubTransport(body)));
        IChatHandler handler = provider switch
        {
            HandlerType.Google => new GoogleChatHandler(factory),
            HandlerType.Anthropic => new AnthropicChatHandler(factory),
            _ => new AlibabaChatHandler(new OpenAiClientFactory(factory))
        };
        var request = new ChatRequest { Model = "test", Messages = [new RelaxKon.Auth.Application.Chat.Message { Role = "user", Content = "hello" }] };
        var route = new ModelKey { Model = "test", SupplierKey = new SupplierKey { ApiKey = "test", BaseUrl = "https://test.invalid" } };
        var chunks = new List<string>();
        await foreach (var text in handler.Stream(request, route, default)) chunks.Add(text);
        Assert.That(string.Concat(chunks), Is.EqualTo("hello"));
    }

    [Test]
    public void Openai_rejects_an_empty_upstream_eof()
    {
        var handler = new OpenAiChatHandler(new OpenAiClientFactory(new StubFactory(new HttpClient(new StubTransport("")))));
        Assert.ThrowsAsync<EndOfStreamException>(async () =>
        {
            await foreach (var _ in handler.Stream(new ChatRequest { Messages = [] },
                new ModelKey { Model = "test", SupplierKey = new SupplierKey { ApiKey = "test", BaseUrl = "https://test.invalid" } }, default)) { }
        });
    }

    private sealed class FakeChat(bool fail) : IChatService
    {
        public async IAsyncEnumerable<string> Stream(ChatRequest request, [EnumeratorCancellation] CancellationToken token)
        { yield return "你好\nreply"; await Task.Yield(); if (fail) throw new InvalidOperationException("private details"); }
    }
    private sealed class FailingHandler(bool partial) : IChatHandler
    {
        public int Calls { get; private set; }
        public HandlerType ProviderType => HandlerType.Openai;
        public async IAsyncEnumerable<string> Stream(ChatRequest request, ModelKey key, [EnumeratorCancellation] CancellationToken token)
        {
            Calls++;
            await Task.Yield();
            if (key.RouteId == 1) { if (partial) yield return "partial"; throw new InvalidOperationException(); }
            yield return "fallback";
        }
    }
    private sealed class StubFactory(HttpClient client) : IHttpClientFactory
    { public HttpClient CreateClient(string name) { Assert.That(name, Is.EqualTo("AiProvider")); return client; } }
    private sealed class StubTransport(string? body = null) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            RequestUri = request.RequestUri;
            Body = await request.Content!.ReadAsStringAsync(token);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body ?? "data: {\"id\":\"test\",\"object\":\"chat.completion.chunk\",\"created\":0,\"model\":\"upstream\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n", Encoding.UTF8, "text/event-stream") };
        }
    }
}
