using System.Net;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Newtonsoft.Json.Linq;
using Turbo.Auth.Application.Media;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Contracts.Media;
using Turbo.Auth.Models.Suppliers;
namespace Turbo.Auth.Tests;
public class APIMartImageTests
{
    [TestCase("gpt-image-2.5-flare")][TestCase("gpt-image-2.5-sunburst")][TestCase("gpt-image-2.6-flare")][TestCase("gpt-image-2.6-sunburst")]
    public async Task Standard_models_serialize_only_documented_options_and_pin_receipt(string model)
    {
        var(fixture,http,store)=Setup();
        var result=await fixture.Generate(7,new(){Model=model,Prompt="Edit product",Size="1600x1200",Resolution="4k",Quality="max",N=2,OutputFormat="webp",OutputCompression=60,Background="transparent",ImageUrls=["https://example.com/product.png"],MaskUrl="https://example.com/mask.png",Complexity=model.Contains("2.6")?"low":null},default);
        var body=JObject.Parse(http.Calls.Single().Body!);
        Assert.That(body["model"]!.Value<string>(),Is.EqualTo(model));Assert.That(body["resolution"],Is.Null);
        Assert.That(body["image_urls"]!.Count(),Is.EqualTo(1));Assert.That(body["output_compression"]!.Value<int>(),Is.EqualTo(60));
        Assert.That(body["moderation"]!.Value<string>(),Is.EqualTo("low"));Assert.That(body["idempotency_key"],Is.Null);
        Assert.That(result.Data!.Single().TaskId,Is.EqualTo("task_1"));Assert.That(store.Items["task_1"],Is.EqualTo((7,(int?)11)));
        Assert.That(http.Calls.Single().Path,Is.EqualTo("/v1/images/generations"));
    }
    [Test]public async Task Ext25_uses_uppercase_tier_and_unified_response_headers()
    {
        var(service,http,store)=Setup();http.Reply="""{"code":202,"data":{"id":"task_unified","status":"pending","poll_url":"https://malicious.invalid"}}""";
        var result=await service.Generate(7,new(){Model="gpt-image-2.5-ext",Version="sunburst",Prompt="Logo",Resolution="2K",Size="16:9",IdempotencyKey="operation_123"},default);
        var call=http.Calls.Single();var json=JObject.Parse(call.Body!);
        Assert.That(call.Version,Is.EqualTo("2026-07-27"));Assert.That(call.Idempotency,Has.Length.EqualTo(64));Assert.That(json["version"]!.Value<string>(),Is.EqualTo("sunburst"));Assert.That(json["quality"],Is.Null);Assert.That(json["output_format"],Is.Null);
        Assert.That(result.Code,Is.EqualTo(200));Assert.That(result.Data!.Single().TaskId,Is.EqualTo("task_unified"));
    }
    [Test]public async Task Idempotency_header_is_account_scoped_and_not_forwarded_in_json()
    {
        var(service,http,store)=Setup();
        await service.Generate(7,new(){Model="gpt-image-2.5-ext",Prompt="Logo",IdempotencyKey="operation_123"},default);
        http.Reply="""{"code":200,"data":[{"task_id":"task_other","status":"submitted"}]}""";
        await service.Generate(8,new(){Model="gpt-image-2.5-ext",Prompt="Logo",IdempotencyKey="operation_123"},default);
        Assert.That(http.Calls[0].Idempotency,Is.Not.EqualTo(http.Calls[1].Idempotency));Assert.That(http.Calls[0].Body,Does.Not.Contain("operation_123"));
    }
    [Test]public async Task Ext26_uses_documented_single_image_tier_and_omits_ignored_options()
    {
        var(service,http,store)=Setup();await service.Generate(7,new(){Model="gpt-image-2.6-ext",Prompt="Logo",Version="SUNBURST",Resolution="1K",N=1,OutputFormat="jpg",Background="transparent",Moderation="low",OutputCompression=20,Quality="MAX",Complexity=" HIGH "},default);
        var body=JObject.Parse(http.Calls.Single().Body!);Assert.That(body["resolution"]!.Value<string>(),Is.EqualTo("1k"));Assert.That(body["output_format"]!.Value<string>(),Is.EqualTo("jpeg"));
        Assert.That(body["background"],Is.Null);Assert.That(body["moderation"],Is.Null);Assert.That(body["output_compression"],Is.Null);Assert.That(body["quality"]!.Value<string>(),Is.EqualTo("max"));Assert.That(body["complexity"]!.Value<string>(),Is.EqualTo("high"));
    }
    [TestCase("refs")][TestCase("mask_without_reference")][TestCase("mask_markup")][TestCase("unsafe_url")][TestCase("private_url")][TestCase("compression_png")][TestCase("svg_missing_model")][TestCase("gif_sheet")][TestCase("gif_refs")][TestCase("ext26_n")][TestCase("ext26_tier")][TestCase("ext25_pixels")][TestCase("ext25_quality")][TestCase("standard_inline_refs")]
    public void Invalid_combinations_are_rejected_locally(string kind)
    {
        var(service,http,store)=Setup();
        var model=kind.StartsWith("ext26")?"gpt-image-2.6-ext":kind.StartsWith("ext25")?"gpt-image-2.5-ext":"gpt-image-2.6-flare";
        var request=new APIMartImageGenerationRequest{Model=model,Prompt="Logo",N=kind=="ext26_n"?2:1,Resolution=kind=="ext26_tier"?"2k":null,Size=kind is "gif_sheet" or "ext25_pixels"?"1024x1024":null,Quality=kind=="ext25_quality"?"high":null,
            ImageUrls=kind=="refs"?Enumerable.Repeat("https://example.com/a.png",17).ToArray():kind=="mask_markup"||kind=="gif_refs"?["https://example.com/a.png"]:kind=="unsafe_url"?["file:///private"]:kind=="private_url"?["http://127.0.0.1/a"]:kind=="standard_inline_refs"?["data:image/png;base64,AQID"]:null,
            MaskUrl=kind is "mask_without_reference" or "mask_markup"?"https://example.com/mask.png":null,MarkupUrl=kind=="mask_markup"?"https://example.com/overlay.png":null,
            OutputCompression=kind=="compression_png"?30:null,OutputFormat=kind=="svg_missing_model"?"svg":kind is "gif_sheet" or "gif_refs"?"gif":null};
        Assert.ThrowsAsync<ImageRequestException>(()=>service.Generate(7,request,default));Assert.That(http.Calls,Is.Empty);
    }
    [Test]public void Gif_sheet_rule_and_svg_author_configuration_match_docs()
    {
        foreach(var count in new[]{2,9,12,16})Assert.DoesNotThrow(()=>APIMartImagePolicy.Build(new(){Model="gpt-image-2.6-flare",Prompt="Loop",OutputFormat="gif",Size="512x512",FrameCount=count}));
        Assert.DoesNotThrow(()=>APIMartImagePolicy.Build(new(){Model="gpt-image-2.6-flare",Prompt="Loop",OutputFormat="gif",Size="768x768",FrameCount=12}));
        Assert.Throws<ImageRequestException>(()=>APIMartImagePolicy.Build(new(){Model="gpt-image-2.6-flare",Prompt="Loop",OutputFormat="gif",Size="768x768",FrameCount=16}));
        Assert.DoesNotThrow(()=>APIMartImagePolicy.Build(new(){Model="gpt-image-2.6-ext",Prompt="Logo",OutputFormat="svg"}));
        var svg=APIMartImagePolicy.Build(new(){Model="gpt-image-2.6-flare",Prompt="Logo",OutputFormat="svg",Svg=new(){Model="gpt-5.4-mini",Reasoning=new(){Effort="low"}}});Assert.That(svg.ContainsKey("svg"),Is.True);
    }
    [Test]public async Task Pinned_task_queries_use_exact_key_and_never_fallback_when_disabled()
    {
        var store=new Receipts();store.Items["task_owned"]=(7,12);
        var http=new Transport{Reply="""{"id":"task_owned","status":"completed","progress":100,"result":{"images":[{"url":["https://example.com/a.svg","javascript:bad"]}]}}"""};
        var routes=Routes(Key(11),Key(12));var service=new APIMartImageService(routes,new Factory(http),store,new EphemeralDataProtectionProvider());
        var result=await service.GetTask(7,"task_owned","zh",default);
        Assert.That(http.Calls.Single().Auth,Is.EqualTo("Bearer secret-12"));Assert.That(result!.Data!.Result!.Images.Single().Url,Is.EqualTo(new[]{"https://example.com/a.svg"}));
        routes.Transfer(Routes(Key(11)));Assert.ThrowsAsync<APIMartProviderException>(()=>service.GetTask(7,"task_owned","en",default));Assert.That(http.Calls,Has.Count.EqualTo(1));
        Assert.That(await service.GetTask(8,"task_owned","en",default),Is.Null);Assert.That(http.Calls,Has.Count.EqualTo(1));
    }
    [Test]public async Task Accepted_task_receipt_recovery_is_signed_account_bound_and_never_regenerates()
    {
        var(service,http,store)=Setup();store.Fail=true;
        var error=Assert.ThrowsAsync<APIMartReceiptPendingException>(()=>service.Generate(7,new(){Prompt="Logo"},default));
        Assert.That(error!.Response.Data!.Single().TaskId,Is.EqualTo("task_1"));
        Assert.ThrowsAsync<ImageRequestException>(()=>service.Recover(8,error.RecoveryToken));Assert.ThrowsAsync<ImageRequestException>(()=>service.Recover(7,error.RecoveryToken+"broken"));
        store.Fail=false;var result=await service.Recover(7,error.RecoveryToken);Assert.That(result.Data!.Single().TaskId,Is.EqualTo("task_1"));Assert.That(store.Items["task_1"].Owner,Is.EqualTo(7));Assert.That(http.Calls,Has.Count.EqualTo(1));
    }
    [Test]public async Task Legacy_null_pin_receipts_keep_historical_task_query()
    {
        var(service,http,store)=Setup();store.Items["task_old"]=(7,null);http.Reply="""{"code":200,"data":{"id":"task_old","status":"failed","error":{"message":"provider secret never reflect","code":"bad"}}}""";
        var result=await service.GetTask(7,"task_old","en",default);Assert.That(result!.Data!.Error!.Message,Is.EqualTo("Image generation failed"));Assert.That(http.Calls,Has.Count.EqualTo(1));
    }
    [Test]public void Same_operation_uses_stable_key_from_an_unchanged_pool()
    {
        var routes=Routes(Key(12),Key(11));var first=routes.GetApiMartKey(operationKey:"same-account-operation")!.SupplierKeyId;
        for(var index=0;index<20;index++)Assert.That(routes.GetApiMartKey(operationKey:"same-account-operation")!.SupplierKeyId,Is.EqualTo(first));
        routes.Transfer(new QuickModel{ApiMartKeys=[Key(11),Key(12)]});Assert.That(routes.GetApiMartKey(operationKey:"same-account-operation")!.SupplierKeyId,Is.EqualTo(first));
    }
    [TestCase("{\"code\":\"invalid\",\"data\":[]}")][TestCase("{\"data\":null}")]
    public void Malformed_submission_responses_are_sanitized(string reply)
    {
        var(service,http,store)=Setup();http.Reply=reply;
        var error=Assert.ThrowsAsync<APIMartProviderException>(()=>service.Generate(7,new(){Prompt="Logo"},default));Assert.That(error!.Code,Is.EqualTo("image_provider_error"));Assert.That(store.Items,Is.Empty);
    }
    [TestCase("{\"id\":\"task_old\",\"status\":\"completed\",\"result\":{\"images\":[{\"url\":null}]}}")]
    [TestCase("{\"id\":\"task_old\",\"status\":\"completed\",\"result\":{\"images\":[null]}}")]
    public void Malformed_task_results_do_not_crash_or_reflect_upstream_data(string reply)
    {
        var(service,http,store)=Setup();store.Items["task_old"]=(7,null);http.Reply=reply;
        Assert.ThrowsAsync<APIMartProviderException>(()=>service.GetTask(7,"task_old","en",default));
    }
    [Test]public async Task Cancelled_tasks_are_normalized_to_terminal_failure()
    {
        var(service,http,store)=Setup();store.Items["task_old"]=(7,null);http.Reply="""{"id":"task_old","status":"cancelled"}""";
        var result=await service.GetTask(7,"task_old","en",default);Assert.That(result!.Data!.Status,Is.EqualTo("failed"));Assert.That(result.Data.Error!.Code,Is.EqualTo("image_task_cancelled"));
    }
    [Test]public void Expired_receipt_tokens_cannot_claim_or_query_tasks()
    {
        var protection=new EphemeralDataProtectionProvider();var store=new Receipts();var http=new Transport();
        var service=new APIMartImageService(Routes(Key(11)),new Factory(http),store,protection);
        var token=protection.CreateProtector("Turbo.APIMart.TaskReceipt.v1").Protect(System.Text.Json.JsonSerializer.Serialize(new {AccountId=7,SupplierKeyId=11,Tasks=new[]{new APIMartTaskData{TaskId="task_expired",Status="submitted"}},ExpiresAt=DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds()}));
        Assert.ThrowsAsync<ImageRequestException>(()=>service.Recover(7,token));Assert.That(store.Items,Is.Empty);Assert.That(http.Calls,Is.Empty);
    }
    [TestCase("{\"n\":1.5}")][TestCase("{\"n\":\"2\"}")][TestCase("{\"output_compression\":50.5}")][TestCase("{\"frame_count\":\"9\"}")]
    public void Fractional_or_string_integer_parameters_cannot_be_silently_coerced(string json)
    {Assert.Throws<Newtonsoft.Json.JsonSerializationException>(()=>Newtonsoft.Json.JsonConvert.DeserializeObject<APIMartImageGenerationRequest>(json));}
    private static (APIMartImageService Service,Transport Http,Receipts Store)Setup()
    {
        var http=new Transport();var store=new Receipts();return(new(Routes(Key(11)),new Factory(http),store,new EphemeralDataProtectionProvider()),http,store);
    }
    private static SupplierKey Key(int id)=>new(){SupplierKeyId=id,Enable=true,RequestIdentifier=(int)HandlerType.ApiMart,BaseUrl="https://provider.invalid",ApiKey="secret-"+id};
    private static QuickModel Routes(params SupplierKey[] keys){var routes=new QuickModel();routes.Transfer(new QuickModel{ApiMartKeys=keys.ToList()});return routes;}
    private sealed class Receipts:IImageTaskReceipts
    {
        public bool Fail;public Dictionary<string,(int Owner,int? Pin)>Items=new();
        public Task RecordTask(int account,string id,CancellationToken token,int? pin=null){if(Fail)throw new IOException();if(Items.TryGetValue(id,out var item)&&item!=(account,pin))throw new InvalidOperationException();Items[id]=(account,pin);return Task.CompletedTask;}
        public Task<bool>OwnsTask(int account,string id,CancellationToken token)=>Task.FromResult(Items.TryGetValue(id,out var item)&&item.Owner==account);
        public Task<int?>GetTaskSupplierKeyId(int account,string id,CancellationToken token)=>Task.FromResult(Items.TryGetValue(id,out var item)&&item.Owner==account?item.Pin:null);
    }
    private sealed class Factory(Transport http):IHttpClientFactory{public HttpClient CreateClient(string name)=>new(http,false);}
    private sealed class Transport:HttpMessageHandler
    {
        public record Call(string Path,string? Body,string? Auth,string? Idempotency,string? Version);
        public List<Call>Calls=new();public string Reply="""{"code":200,"data":[{"status":"submitted","task_id":"task_1"}]}""";
        protected override async Task<HttpResponseMessage>SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Calls.Add(new(request.RequestUri!.AbsolutePath,request.Content is null?null:await request.Content.ReadAsStringAsync(token),request.Headers.Authorization?.ToString(),request.Headers.TryGetValues("Idempotency-Key",out var key)?key.Single():null,request.Headers.TryGetValues("X-APIMart-Response-Version",out var version)?version.Single():null));
            return new(HttpStatusCode.OK){Content=new StringContent(Reply,Encoding.UTF8,"application/json")};
        }
    }
}
