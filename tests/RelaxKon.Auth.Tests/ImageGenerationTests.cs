using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using RelaxKon.Auth.Application.Media;
using RelaxKon.Auth.Application.Providers;
using RelaxKon.Auth.Application.Routing;
using RelaxKon.Auth.Contracts.Media;
using RelaxKon.Auth.Controllers.AI.OpenAI;
using RelaxKon.Auth.Models.Suppliers;
namespace RelaxKon.Auth.Tests;

public class ImageGenerationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Synchronous_images_never_use_apimart_routes(bool openaiAvailable)
    {
        var transport = new Transport();
        var routes = new QuickModel();
        var weights = new List<WeightKey> {
            new() { RouteId=1, Priority=0, Weight=1, ProviderModelValue="gpt-image-2.6-flare",
                SupplierKey=new SupplierKey { RequestIdentifier=(int)HandlerType.ApiMart, ApiKey="wrong-protocol", BaseUrl="https://apimart.invalid/v1" } }
        };
        if (openaiAvailable) weights.Add(new() { RouteId=2, Priority=1, Weight=1, ProviderModelValue="gpt-image-2.6-flare",
            SupplierKey=new SupplierKey { RequestIdentifier=(int)HandlerType.Openai, ApiKey="test-secret", BaseUrl="https://openai.invalid/v1" } });
        routes.Transfer(new QuickModel { Quick=new() { ["gpt-image-2.6-flare"]=weights } });
        var service = new MediaService(routes, new OpenAiClientFactory(new Factory(transport)));
        var request = new ImageGenerationRequest {Model="gpt-image-2.6-flare",Prompt="icon"};
        if (openaiAvailable) {
            await service.Image(request, false, default);
            Assert.That(transport.Uri!.Host, Is.EqualTo("openai.invalid"));
        } else {
            var result = await new MediaController(service).Gpt_image(request, default);
            var problem = (ObjectResult)result.Result!;
            Assert.That(problem.StatusCode, Is.EqualTo(503));
            Assert.That(((ProblemDetails)problem.Value!).Extensions["code"], Is.EqualTo("image_route_unavailable"));
            Assert.That(transport.Calls, Is.Zero);
        }
    }

    [TestCase("gpt-image-2.6-sunburst")]
    [TestCase("gpt-image-2.6-flare")]
    public async Task New_models_keep_routed_alias_and_only_supported_wire_options(string model)
    {
        var transport = new Transport();
        var result = await Media(transport, model, "private-image-route").Image(new ImageGenerationRequest {
            Model=model,Prompt="A poster",Size="1536x864",N=2,Quality="max",Complexity="medium",
            OutputFormat="webp",OutputCompression=75,Background="transparent"
        },false,default);
        var body = JObject.Parse(transport.Body!);
        Assert.Multiple(()=> {
            Assert.That(transport.Uri!.AbsolutePath,Is.EqualTo("/v1/images/generations"));
            Assert.That(transport.Authorization,Is.EqualTo("Bearer test-secret"));
            Assert.That(body["model"]!.Value<string>(),Is.EqualTo("private-image-route"));
            Assert.That(body["complexity"]!.Value<string>(),Is.EqualTo("medium"));
            Assert.That(body["output_compression"]!.Value<int>(),Is.EqualTo(75));
            Assert.That(body["quality"]!.Value<string>(),Is.EqualTo("max"));
            Assert.That(body["style"],Is.Null); Assert.That(body["response_format"],Is.Null);
            Assert.That(result.OutputFormat,Is.EqualTo("webp"));
            Assert.That(result.Data.Single().Base64,Is.EqualTo("AQID"));
        });
    }
    [Test] public async Task Recognized_upstream_model_supplies_capabilities_for_friendly_logical_name()
    {
        var transport = new Transport();
        await Media(transport,"Studio","gpt-image-2.6-flare").Image(new ImageGenerationRequest {
            Model="Studio",Prompt="logo",Complexity="high",OutputFormat="svg"
        },false,default);
        Assert.That(JObject.Parse(transport.Body!)["complexity"]!.Value<string>(),Is.EqualTo("high"));
    }
    [TestCase("gpt-image-2.5-sunburst")]
    [TestCase("gpt-image-2.5-flare")]
    public async Task Older_enhanced_models_strip_complexity_and_inapplicable_compression(string model)
    {
        var transport = new Transport();
        await Media(transport,model).Image(new ImageGenerationRequest {
            Model=model,Prompt="icon",Quality="xhigh",Complexity="high",OutputFormat="png",OutputCompression=45,FrameCount=8
        },false,default);
        var body=JObject.Parse(transport.Body!);
        Assert.That(body["complexity"],Is.Null); Assert.That(body["output_compression"],Is.Null); Assert.That(body["frame_count"],Is.Null);
        Assert.That(body["quality"]!.Value<string>(),Is.EqualTo("xhigh"));
    }
    [TestCase("dall-e-2")][TestCase("dall-e-3")][TestCase("gpt-image-1")][TestCase("gpt-image-1.5")][TestCase("gpt-image-1-mini")][TestCase("gpt-image-2")]
    public async Task Removed_models_are_rejected_before_provider_calls(string model)
    {
        var transport=new Transport();
        Assert.ThrowsAsync<ImageRequestException>(()=>Media(transport,model).Image(new ImageGenerationRequest {Model=model,Prompt="icon"},false,default));
        Assert.That(transport.Calls,Is.EqualTo(0)); await Task.CompletedTask;
    }
    [Test] public void Removed_model_is_rejected_even_without_configured_routes()
    {
        var transport=new Transport();
        var service=new MediaService(new QuickModel(),new OpenAiClientFactory(new Factory(transport)));
        var error=Assert.ThrowsAsync<ImageRequestException>(()=>service.Image(new ImageGenerationRequest {Model="gpt-image-2",Prompt="icon"},false,default));
        Assert.That(error!.Field,Is.EqualTo("model")); Assert.That(transport.Calls,Is.EqualTo(0));
    }
    [Test] public async Task Legacy_endpoint_is_gone_without_calling_provider()
    {
        var transport=new Transport();
        var response=new MediaController(Media(transport,"gpt-image-2.6-flare")).Dall_E();
        Assert.That(((ObjectResult)response.Result!).StatusCode,Is.EqualTo(410)); Assert.That(transport.Calls,Is.EqualTo(0)); await Task.CompletedTask;
    }
    [Test] public async Task Unknown_model_is_not_assumed_to_support_gpt_image()
    {
        var transport=new Transport();
        Assert.ThrowsAsync<ImageRequestException>(()=>Media(transport,"unknown-gpt-image").Image(new ImageGenerationRequest {Model="unknown-gpt-image",Prompt="icon"},false,default));
        Assert.That(transport.Calls,Is.EqualTo(0)); await Task.CompletedTask;
    }
    [Test] public async Task Gif_options_are_sent_without_raster_compression()
    {
        var transport = new Transport();
        await Media(transport,"gpt-image-2.6-flare").Image(new ImageGenerationRequest {
            Model="gpt-image-2.6-flare",Prompt="loop",Size="auto",OutputFormat="gif",FrameCount=12,FrameDurationMs=250,OutputCompression=50
        },false,default);
        var body=JObject.Parse(transport.Body!);
        Assert.That(body["frame_count"]!.Value<int>(),Is.EqualTo(12)); Assert.That(body["frame_duration_ms"]!.Value<int>(),Is.EqualTo(250)); Assert.That(body["output_compression"],Is.Null);
    }
    [TestCase("size")][TestCase("background")][TestCase("quality")][TestCase("svg_count")][TestCase("gif_frames")]
    public async Task Invalid_combinations_fail_before_any_provider_call(string invalid)
    {
        var transport=new Transport();
        var request=new ImageGenerationRequest {
            Model="gpt-image-2.6-flare",Prompt="icon",
            Size=invalid=="size"?"123x100":"auto",Background=invalid=="background"?"transparent":null,
            Quality=invalid=="quality"?"hd":null,OutputFormat=invalid=="background"?"jpeg":invalid=="svg_count"?"svg":invalid=="gif_frames"?"gif":"png",
            N=invalid=="svg_count"?2:1,FrameCount=invalid=="gif_frames"?64:null
        };
        Assert.ThrowsAsync<ImageRequestException>(()=>Media(transport,request.Model).Image(request,false,default));
        Assert.That(transport.Calls,Is.EqualTo(0)); await Task.CompletedTask;
    }
    [Test] public void Known_actual_upstream_takes_precedence_over_newer_logical_name()
    {
        Assert.Throws<ImageRequestException>(()=>ImageGenerationPolicy.Build(new ImageGenerationRequest {
            Model="gpt-image-2.6-flare",Prompt="icon",Quality="max"
        },"gpt-image-1.5",false));
    }
    [Test] public async Task Output_is_allowlisted_and_invalid_url_or_format_cannot_be_reflected()
    {
        var transport = new Transport { Reply="""{"created":"invalid","output_format":"text/html","provider_secret":"never return","data":[{"url":"javascript:alert(1)","b64_json":"AQID","unknown":"hidden"}],"usage":{"input_tokens":"invalid","output_tokens":2,"total_tokens":2}}""" };
        var result = await Media(transport,"gpt-image-2.6-flare").Image(new ImageGenerationRequest {Model="gpt-image-2.6-flare",Prompt="icon",OutputFormat="svg"},false,default);
        Assert.That(result.OutputFormat,Is.EqualTo("svg")); Assert.That(result.Data.Single().Url,Is.Null); Assert.That(result.Created,Is.EqualTo(0));
        Assert.That(result.Usage!.InputTokens,Is.EqualTo(0));
        Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(result),Does.Not.Contain("provider_secret").And.Not.Contain("text/html").And.Not.Contain("javascript"));
    }
    [Test] public async Task Controller_returns_safe_errors_without_provider_secrets()
    {
        var transport = new Transport {Status=HttpStatusCode.Unauthorized,Reply="""{"error":{"message":"secret provider token ABC","type":"invalid_api_key"}}"""};
        var response=await new MediaController(Media(transport,"gpt-image-2.6-flare")).Gpt_image(new ImageGenerationRequest {Model="gpt-image-2.6-flare",Prompt="icon"},default);
        var result=(ObjectResult)response.Result!;
        Assert.That(result.StatusCode,Is.EqualTo(502)); Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(result.Value),Does.Not.Contain("ABC").And.Not.Contain("secret provider"));
        Assert.That(transport.Calls,Is.EqualTo(1));
    }
    [TestCase("not json")][TestCase("[]")][TestCase("{\"data\":[]}")]
    public async Task Malformed_provider_response_returns_safe_bad_gateway(string reply)
    {
        var transport=new Transport {Reply=reply};
        var response=await new MediaController(Media(transport,"gpt-image-2.6-flare")).Gpt_image(new ImageGenerationRequest {Model="gpt-image-2.6-flare",Prompt="icon"},default);
        Assert.That(((ObjectResult)response.Result!).StatusCode,Is.EqualTo(502));
    }
    [Test] public void Apimart_retired_creation_endpoints_do_not_require_an_upstream_client()
    {
        var controller=new RelaxKon.Auth.Controllers.AI.APIMart.APIMartGPTImageController();
        Assert.That(((ObjectResult)controller.GPTImage2Generate().Result!).StatusCode,Is.EqualTo(410));
        Assert.That(((ObjectResult)controller.GPTImage2OfficialGenerate().Result!).StatusCode,Is.EqualTo(410));
    }
    private static MediaService Media(Transport transport,string logical,string? upstream=null)
    {
        var routes=new QuickModel(); routes.Transfer(new QuickModel{Quick=new(){[logical]=[new WeightKey{RouteId=1,Weight=1,ProviderModelValue=upstream??logical,SupplierKey=new SupplierKey{ApiKey="test-secret",BaseUrl="https://provider.invalid"}}]}});
        return new(routes,new OpenAiClientFactory(new Factory(transport)));
    }
    private sealed class Factory(Transport transport):IHttpClientFactory
    { public HttpClient CreateClient(string name)=>new(transport,disposeHandler:false); }
    private sealed class Transport:HttpMessageHandler
    {
        public string? Body; public Uri? Uri; public string? Authorization; public int Calls;
        public HttpStatusCode Status=HttpStatusCode.OK;
        public string Reply="""{"created":123,"data":[{"b64_json":"AQID","revised_prompt":"Revised"}]}""";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Calls++; Uri=request.RequestUri; Authorization=request.Headers.Authorization?.ToString(); Body=await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(Status){Content=new StringContent(Reply,Encoding.UTF8,"application/json")};
        }
    }
}
