using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Newtonsoft.Json.Linq;
using RelaxKon.Auth.Application.Routing;
using RelaxKon.Auth.Contracts.Media;
using RelaxKon.Auth.Models.Suppliers;
namespace RelaxKon.Auth.Application.Media;
public interface IImageTaskReceipts
{
    Task RecordTask(int accountId,string taskId,CancellationToken token,int? supplierKeyId=null);
    Task<bool> OwnsTask(int accountId,string taskId,CancellationToken token);
    Task<int?> GetTaskSupplierKeyId(int accountId,string taskId,CancellationToken token);
}
public sealed class APIMartProviderException(string code,int status=502):Exception(code)
{
    public string Code {get;}=code; public int Status{get;}=status;
}
public sealed class APIMartReceiptPendingException(APIMartGenerationResponse response,string recoveryToken):Exception("Task receipt pending")
{
    public APIMartGenerationResponse Response{get;}=response; public string RecoveryToken{get;}=recoveryToken;
}
public sealed class APIMartImageService(QuickModel routes,IHttpClientFactory http,IImageTaskReceipts receipts,IDataProtectionProvider protection)
{
    private sealed record Receipt(int AccountId,int SupplierKeyId,APIMartTaskData[] Tasks,long ExpiresAt);
    private readonly IDataProtector protector=protection.CreateProtector("Turbo.APIMart.TaskReceipt.v1");
    private static bool TaskId(string? id)=>id is not null&&Regex.IsMatch(id,"^[A-Za-z0-9_-]{1,255}$");
    private static Uri Endpoint(SupplierKey key,string path)
    {
        if(!Uri.TryCreate(key.BaseUrl?.Trim().TrimEnd('/'),UriKind.Absolute,out var uri)||uri.Scheme is not("http" or "https")||!string.IsNullOrEmpty(uri.UserInfo))throw new APIMartProviderException("image_route_unavailable",503);
        var root=uri.ToString().TrimEnd('/'); if(!root.EndsWith("/v1",StringComparison.OrdinalIgnoreCase))root+="/v1";
        return new Uri(root+path);
    }
    private static APIMartProviderException ProviderError(int status)=>new(status==429?"image_provider_busy":status is 400 or 403 or 402?"image_provider_rejected":"image_provider_error",status==429?429:status is 400 or 403 or 402?400:502);
    private HttpClient Client(SupplierKey key)
    {
        var client=http.CreateClient("AiProvider");client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",key.ApiKey);return client;
    }
    public async Task<APIMartGenerationResponse> Generate(int accountId,APIMartImageGenerationRequest request,CancellationToken token)
    {
        var payload=APIMartImagePolicy.Build(request);
        var operation=request.IdempotencyKey is null?null:Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accountId+":"+request.IdempotencyKey)));
        var key=routes.GetApiMartKey(operationKey:operation)??throw new APIMartProviderException("image_route_unavailable",503);
        using var client=Client(key);
        using var message=new HttpRequestMessage(HttpMethod.Post,Endpoint(key,"/images/generations")){Content=new StringContent(JsonSerializer.Serialize(payload),Encoding.UTF8,"application/json")};
        if(request.Model=="gpt-image-2.5-ext")message.Headers.Add("X-APIMart-Response-Version","2026-07-27");
        if(request.IdempotencyKey is not null)
            message.Headers.Add("Idempotency-Key",operation);
        // No automatic generation retry: an upstream timeout may have accepted a billable task.
        using var response=await client.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,token);
        if(!response.IsSuccessStatusCode)throw ProviderError((int)response.StatusCode);
        APIMartTaskData[] tasks;
        try
        {
            using var acceptedRead=new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var json=JObject.Parse(await response.Content.ReadAsStringAsync(acceptedRead.Token));
            var code=json["code"]?.Value<int?>();if(code is not null&&code!=200&&code!=202)throw ProviderError(code.Value);
            var data=json["data"];
            if(data is JArray array)tasks=array.OfType<JObject>().Select(item=>new APIMartTaskData{TaskId=item["task_id"]?.Value<string>()??"",Status=item["status"]?.Value<string>()??"submitted"}).ToArray();
            else if(data is JObject item)tasks=[new(){TaskId=item["id"]?.Value<string>()??"",Status=item["status"]?.Value<string>()??"pending"}];
            else throw new APIMartProviderException("image_provider_error");
            if(tasks.Length is <1 or >4||tasks.Any(item=>!TaskId(item.TaskId))||tasks.Select(item=>item.TaskId).Distinct().Count()!=tasks.Length)throw new APIMartProviderException("image_provider_error");
            foreach(var task in tasks)if(task.Status is not("pending" or "submitted" or "processing"))task.Status="submitted";
        }
        catch(Exception error)when(error is Newtonsoft.Json.JsonException or FormatException or InvalidCastException or OverflowException){throw new APIMartProviderException("image_provider_error");}
        var receipt=new Receipt(accountId,key.SupplierKeyId,tasks,DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds());
        await SaveReceipt(receipt);
        return Submission(tasks);
    }
    private static APIMartGenerationResponse Submission(APIMartTaskData[] tasks)=>new(){Code=200,Data=tasks.ToList()};
    private async Task SaveReceipt(Receipt receipt)
    {
        var recovery=protector.Protect(JsonSerializer.Serialize(receipt));
        try
        {
            // Finish registering accepted work even if its HTTP caller disconnects.
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
            foreach(var task in receipt.Tasks)await receipts.RecordTask(receipt.AccountId,task.TaskId,timeout.Token,receipt.SupplierKeyId);
        }
        catch(Exception){throw new APIMartReceiptPendingException(Submission(receipt.Tasks),recovery);}
    }
    public async Task<APIMartGenerationResponse> Recover(int accountId,string recoveryToken)
    {
        Receipt? receipt;
        try{receipt=JsonSerializer.Deserialize<Receipt>(protector.Unprotect(recoveryToken));}
        catch(Exception error)when(error is CryptographicException or System.Text.Json.JsonException or ArgumentException){throw new ImageRequestException("recovery_token");}
        if(receipt is null||receipt.AccountId!=accountId||receipt.ExpiresAt<DateTimeOffset.UtcNow.ToUnixTimeSeconds()||receipt.Tasks.Length is <1 or >4||receipt.Tasks.Any(item=>!TaskId(item.TaskId)))throw new ImageRequestException("recovery_token");
        await SaveReceipt(receipt);return Submission(receipt.Tasks);
    }
    public async Task<TaskResponse?> GetTask(int accountId,string taskId,string? language,CancellationToken token)
    {
        if(!TaskId(taskId)||!await receipts.OwnsTask(accountId,taskId,token))return null;
        var pin=await receipts.GetTaskSupplierKeyId(accountId,taskId,token);
        var key=routes.GetApiMartKey(pin)??throw new APIMartProviderException("image_route_unavailable",503);
        using var client=Client(key);
        var lang=new[]{"en","zh","ja","ko","ru","fr","de","id","pt","es"}.Contains(language)?language:"en";
        using var response=await client.GetAsync(Endpoint(key,"/tasks/"+Uri.EscapeDataString(taskId)+"?language="+lang),token);
        if(!response.IsSuccessStatusCode)throw ProviderError((int)response.StatusCode);
        try
        {
            var json=JObject.Parse(await response.Content.ReadAsStringAsync(token));
            var data=json["data"]as JObject??json;
            if(data["id"]?.Value<string>()!=taskId)throw new APIMartProviderException("image_provider_error");
            var task=data.ToObject<TaskData>()??throw new APIMartProviderException("image_provider_error");
            if(task.Status is not("pending" or "submitted" or "processing" or "completed" or "failed" or "cancelled"))throw new APIMartProviderException("image_provider_error");
            task.Progress=Math.Clamp(task.Progress,0,100);
            var cancelled=task.Status=="cancelled";
            if(cancelled){task.Status="failed";task.Error=new(){Code="image_task_cancelled",Message="Image generation cancelled",Type="image_generation_error"};}
            if(task.Result is not null)
            {
                if(task.Result.Images is null||task.Result.Images.Any(image=>image is null||image.Url is null))throw new APIMartProviderException("image_provider_error");
                foreach(var image in task.Result.Images)
                    image.Url=image.Url.Where(url=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme is "https" or "http"&&string.IsNullOrEmpty(uri.UserInfo)).ToList();
            }
            if(task.Error is not null)task.Error=new(){Code=cancelled?"image_task_cancelled":"image_task_failed",Message=cancelled?"Image generation cancelled":"Image generation failed",Type="image_generation_error"};
            return new(){Code=200,Data=task};
        }
        catch(Exception error)when(error is Newtonsoft.Json.JsonException or FormatException or InvalidCastException or OverflowException){throw new APIMartProviderException("image_provider_error");}
    }
}
