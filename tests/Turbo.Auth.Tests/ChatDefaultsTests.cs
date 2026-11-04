using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Turbo.Auth.Contracts.Chat;

namespace Turbo.Auth.Tests;

public class ChatDefaultsTests
{
    [Test]
    public void Legacy_generation_parameters_are_not_bound_or_forwarded()
    {
        const string legacy = """
            {"model":"gpt-5","stream":true,"vision":true,
             "temperature":0.6,"top_p":0.6,"max_completion_tokens":4000,
             "presence_penalty":0.6,"frequency_penalty":0.6,
             "reasoning_effort":"high","verbosity":"high","thinking_budget":512,
             "messages":[{"role":"user","content":"hello"}]}
            """;
        var body = JsonConvert.DeserializeObject<ChatCompletionRequest>(legacy)!;
        var forwarded = JObject.FromObject(body, JsonSerializer.Create(new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() }));
        Assert.That(forwarded.Properties().Select(property => property.Name),
            Is.EquivalentTo(new[] { "model", "messages", "stream", "vision" }));
        Assert.That(body.Model, Is.EqualTo("gpt-5"));
        Assert.That(body.Messages, Has.Length.EqualTo(1));
    }
}
